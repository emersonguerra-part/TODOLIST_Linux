using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using TodoListApp.Models;

namespace TodoListApp.Services
{
    public class SyncCoordinator
    {
        private readonly FirebaseService _firebaseService;
        private readonly TodoStorageService _storageService;
        private readonly SemaphoreSlim _syncLock = new(1, 1);
        private CancellationTokenSource? _debounceCts;

        public event Action? DataChangedFromCloud;

        public SyncCoordinator(FirebaseService firebaseService, TodoStorageService storageService)
        {
            _firebaseService = firebaseService;
            _storageService = storageService;
        }

        public async Task<bool> SyncAllAsync()
        {
            if (!_firebaseService.IsLoggedIn)
            {
                _firebaseService.SetStatus(SyncStatus.Disconnected);
                return false;
            }

            if (!await _syncLock.WaitAsync(0))
            {
                // Já está sincronizando
                return false;
            }

            _firebaseService.SetStatus(SyncStatus.Syncing, "Sincronizando com a nuvem...");

            try
            {
                bool profilesChanged = false;
                bool todosChanged = false;

                // 1. SINCRONIZAÇÃO DE PERFIS COM MERGE SEGURO
                var (remoteProfilesJson, remoteProfilesUpdated) = await _firebaseService.DownloadDocumentAsync("data", "profiles");
                var localProfileData = _storageService.LoadProfileData();
                string? localProfilesJson = _storageService.GetRawProfilesJson();
                DateTime localProfilesUpdated = _storageService.GetProfilesLastWriteTimeUtc();

                if (!string.IsNullOrWhiteSpace(remoteProfilesJson))
                {
                    ProfileStorageData? remoteProfileData = null;
                    try
                    {
                        remoteProfileData = JsonSerializer.Deserialize<ProfileStorageData>(remoteProfilesJson);
                    }
                    catch { }

                    if (remoteProfileData != null)
                    {
                        // Mescla perfis por Id
                        var allProfilesDict = new Dictionary<string, UserProfile>();
                        foreach (var p in remoteProfileData.Profiles)
                            allProfilesDict[p.Id] = p;
                        foreach (var p in localProfileData.Profiles)
                            allProfilesDict[p.Id] = p;

                        var mergedProfiles = allProfilesDict.Values.ToList();
                        string? activeId = localProfileData.ActiveProfileId;
                        if (string.IsNullOrWhiteSpace(activeId) || !allProfilesDict.ContainsKey(activeId))
                        {
                            activeId = remoteProfileData.ActiveProfileId ?? mergedProfiles.FirstOrDefault()?.Id;
                        }

                        var mergedData = new ProfileStorageData
                        {
                            ActiveProfileId = activeId,
                            Profiles = mergedProfiles
                        };

                        string mergedJson = JsonSerializer.Serialize(mergedData, new JsonSerializerOptions { WriteIndented = true });
                        if (mergedJson != localProfilesJson)
                        {
                            DateTime bestTime = localProfilesUpdated > (remoteProfilesUpdated ?? DateTime.MinValue) ? localProfilesUpdated : (remoteProfilesUpdated ?? DateTime.UtcNow);
                            _storageService.SaveRawProfilesJson(mergedJson, bestTime);
                            profilesChanged = true;
                        }

                        if (mergedProfiles.Count > remoteProfileData.Profiles.Count)
                        {
                            await _firebaseService.UploadDocumentAsync("data", "profiles", mergedJson, DateTime.UtcNow);
                        }
                    }
                    else
                    {
                        _storageService.SaveRawProfilesJson(remoteProfilesJson, remoteProfilesUpdated);
                        profilesChanged = true;
                    }
                }
                else if (localProfileData.Profiles.Count > 0)
                {
                    await _firebaseService.UploadDocumentAsync("data", "profiles", localProfilesJson ?? "", localProfilesUpdated);
                }

                // 2. SINCRONIZAÇÃO DE TAREFAS COM PROTEÇÃO CONTRA SOBREPOSIÇÃO VAZIA
                var remoteDocs = await _firebaseService.ListDocumentsAsync("todos");
                var remoteDict = remoteDocs.ToDictionary(d => d.DocumentId, d => (d.JsonContent, d.UpdatedAt));

                var localProfileIds = new HashSet<string>(_storageService.GetAllStoredProfileIds());
                var currentProfiles = _storageService.LoadProfileData();
                foreach (var p in currentProfiles.Profiles)
                    localProfileIds.Add(p.Id);
                if (!string.IsNullOrWhiteSpace(currentProfiles.ActiveProfileId))
                    localProfileIds.Add(currentProfiles.ActiveProfileId);

                string? localDefaultJson = _storageService.GetRawTodosJson(null);
                if (!string.IsNullOrWhiteSpace(localDefaultJson))
                {
                    localProfileIds.Add("default");
                }

                var allTargetIds = new HashSet<string>(remoteDict.Keys);
                foreach (var id in localProfileIds)
                {
                    allTargetIds.Add(id);
                }

                foreach (var id in allTargetIds)
                {
                    string? profileIdParam = (id == "default") ? null : id;
                    bool remoteHas = remoteDict.TryGetValue(id, out var remoteData);
                    string? localJson = _storageService.GetRawTodosJson(profileIdParam);
                    DateTime localUpdated = _storageService.GetTodosLastWriteTimeUtc(profileIdParam);

                    bool localIsEmpty = string.IsNullOrWhiteSpace(localJson) || localJson.Trim() == "[]";
                    bool remoteIsEmpty = !remoteHas || string.IsNullOrWhiteSpace(remoteData.JsonContent) || remoteData.JsonContent.Trim() == "[]";

                    if (remoteHas && !string.IsNullOrWhiteSpace(remoteData.JsonContent))
                    {
                        if (localIsEmpty && !remoteIsEmpty)
                        {
                            // Remoto tem tarefas e local está vazio: baixa do remoto
                            _storageService.SaveRawTodosJson(remoteData.JsonContent, profileIdParam, remoteData.UpdatedAt);
                            todosChanged = true;
                        }
                        else if (!localIsEmpty && remoteIsEmpty)
                        {
                            // Local tem tarefas e remoto está vazio: envia para a nuvem
                            await _firebaseService.UploadDocumentAsync("todos", id, localJson!, localUpdated);
                        }
                        else if (!localIsEmpty && !remoteIsEmpty)
                        {
                            // Ambos têm tarefas: compara timestamp
                            if (remoteData.UpdatedAt > localUpdated.AddSeconds(2))
                            {
                                _storageService.SaveRawTodosJson(remoteData.JsonContent, profileIdParam, remoteData.UpdatedAt);
                                todosChanged = true;
                            }
                            else if (localUpdated > remoteData.UpdatedAt.AddSeconds(2))
                            {
                                await _firebaseService.UploadDocumentAsync("todos", id, localJson!, localUpdated);
                            }
                        }
                    }
                    else if (!localIsEmpty)
                    {
                        await _firebaseService.UploadDocumentAsync("todos", id, localJson!, localUpdated);
                    }
                }

                _firebaseService.SetStatus(SyncStatus.Synced, "Tudo sincronizado");

                if (profilesChanged || todosChanged)
                {
                    DataChangedFromCloud?.Invoke();
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro no SyncAll: {ex.Message}");
                _firebaseService.SetStatus(SyncStatus.Error, "Falha na sincronização");
                return false;
            }
            finally
            {
                _syncLock.Release();
            }
        }

        public void ScheduleDebouncedSync()
        {
            if (!_firebaseService.IsLoggedIn)
                return;

            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(1500, token);
                    if (!token.IsCancellationRequested)
                    {
                        await SyncAllAsync();
                    }
                }
                catch (TaskCanceledException)
                {
                }
            }, token);
        }
    }
}
