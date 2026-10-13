using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using TodoListApp.Models;

namespace TodoListApp.Services
{
    public class TodoStorageService
    {
        private readonly string _appDataDir;
        private readonly string _defaultTodosPath;
        private readonly string _profilesPath;
        private readonly string _themePath;

        public TodoStorageService()
        {
            _appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TodoListApp"
            );

            if (!Directory.Exists(_appDataDir))
            {
                Directory.CreateDirectory(_appDataDir);
            }

            _defaultTodosPath = Path.Combine(_appDataDir, "todos.json");
            _profilesPath = Path.Combine(_appDataDir, "profiles.json");
            _themePath = Path.Combine(_appDataDir, "theme.json");
        }

        public string GetProfileTodosPath(string profileId)
        {
            return Path.Combine(_appDataDir, $"todos_{profileId}.json");
        }

        public ProfileStorageData LoadProfileData()
        {
            try
            {
                if (File.Exists(_profilesPath))
                {
                    string json = File.ReadAllText(_profilesPath);
                    var data = JsonSerializer.Deserialize<ProfileStorageData>(json);
                    return data ?? new ProfileStorageData();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao carregar perfis: {ex.Message}");
            }

            return new ProfileStorageData();
        }

        public void SaveProfileData(ProfileStorageData data)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(data, options);
                File.WriteAllText(_profilesPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao salvar perfis: {ex.Message}");
            }
        }

        public List<TodoItem> LoadTodos(string? profileId = null)
        {
            try
            {
                string targetPath = string.IsNullOrWhiteSpace(profileId) 
                    ? _defaultTodosPath 
                    : GetProfileTodosPath(profileId);

                if (File.Exists(targetPath))
                {
                    string json = File.ReadAllText(targetPath);
                    var items = JsonSerializer.Deserialize<List<TodoItem>>(json);
                    return items ?? new List<TodoItem>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao carregar tarefas: {ex.Message}");
            }

            return new List<TodoItem>();
        }

        public void SaveTodos(IEnumerable<TodoItem> todos, string? profileId = null)
        {
            try
            {
                string targetPath = string.IsNullOrWhiteSpace(profileId) 
                    ? _defaultTodosPath 
                    : GetProfileTodosPath(profileId);

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                string json = JsonSerializer.Serialize(todos, options);
                File.WriteAllText(targetPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao salvar tarefas: {ex.Message}");
            }
        }

        public void DeleteProfileData(string profileId)
        {
            try
            {
                string targetPath = GetProfileTodosPath(profileId);
                if (File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao deletar tarefas do perfil: {ex.Message}");
            }
        }

        public DateTime GetProfilesLastWriteTimeUtc()
        {
            if (File.Exists(_profilesPath))
                return File.GetLastWriteTimeUtc(_profilesPath);
            return DateTime.MinValue;
        }

        public DateTime GetTodosLastWriteTimeUtc(string? profileId = null)
        {
            string targetPath = string.IsNullOrWhiteSpace(profileId) ? _defaultTodosPath : GetProfileTodosPath(profileId);
            if (File.Exists(targetPath))
                return File.GetLastWriteTimeUtc(targetPath);
            return DateTime.MinValue;
        }

        public string? GetRawProfilesJson()
        {
            if (File.Exists(_profilesPath))
                return File.ReadAllText(_profilesPath);
            return null;
        }

        public void SaveRawProfilesJson(string json, DateTime? updatedAtUtc = null)
        {
            File.WriteAllText(_profilesPath, json);
            if (updatedAtUtc.HasValue)
            {
                try { File.SetLastWriteTimeUtc(_profilesPath, updatedAtUtc.Value); } catch { }
            }
        }

        public string? GetRawTodosJson(string? profileId = null)
        {
            string targetPath = string.IsNullOrWhiteSpace(profileId) ? _defaultTodosPath : GetProfileTodosPath(profileId);
            if (File.Exists(targetPath))
                return File.ReadAllText(targetPath);
            return null;
        }

        public void SaveRawTodosJson(string json, string? profileId = null, DateTime? updatedAtUtc = null)
        {
            string targetPath = string.IsNullOrWhiteSpace(profileId) ? _defaultTodosPath : GetProfileTodosPath(profileId);
            File.WriteAllText(targetPath, json);
            if (updatedAtUtc.HasValue)
            {
                try { File.SetLastWriteTimeUtc(targetPath, updatedAtUtc.Value); } catch { }
            }
        }

        public List<string> GetAllStoredProfileIds()
        {
            var list = new List<string>();
            try
            {
                var files = Directory.GetFiles(_appDataDir, "todos_*.json");
                foreach (var f in files)
                {
                    string filename = Path.GetFileNameWithoutExtension(f);
                    if (filename.StartsWith("todos_"))
                    {
                        string id = filename.Substring("todos_".Length);
                        if (!string.IsNullOrWhiteSpace(id))
                            list.Add(id);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao listar perfis locais: {ex.Message}");
            }
            return list;
        }

        public bool LoadThemePreference()
        {
            try
            {
                if (File.Exists(_themePath))
                {
                    string content = File.ReadAllText(_themePath);
                    return bool.TryParse(content.Trim(), out bool isDark) && isDark;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao carregar tema: {ex.Message}");
            }
            return false;
        }

        public void SaveThemePreference(bool isDark)
        {
            try
            {
                File.WriteAllText(_themePath, isDark.ToString());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao salvar tema: {ex.Message}");
            }
        }
    }
}
