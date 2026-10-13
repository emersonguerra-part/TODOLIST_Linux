using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TodoListApp.Models;

namespace TodoListApp.Services
{
    public enum SyncStatus
    {
        Disconnected,
        Syncing,
        Synced,
        Error
    }

    public class FirebaseService
    {
        private const string ApiKey = "AIzaSyAkEfmVLOtpArCVH8_2m7Z2DOk--93x2a0";
        private const string ProjectId = "todolist-emerson-29c28";

        private readonly HttpClient _httpClient;
        private readonly string _sessionFilePath;
        private FirebaseSession? _currentSession;

        public event Action<SyncStatus, string?>? SyncStatusChanged;
        public SyncStatus CurrentStatus { get; private set; } = SyncStatus.Disconnected;
        public DateTime? LastSyncTime { get; private set; }

        public FirebaseSession? CurrentSession => _currentSession;
        public bool IsLoggedIn => _currentSession != null && !string.IsNullOrWhiteSpace(_currentSession.IdToken);

        public FirebaseService()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(15)
            };

            string appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TodoListApp"
            );
            if (!Directory.Exists(appDataDir))
            {
                Directory.CreateDirectory(appDataDir);
            }

            _sessionFilePath = Path.Combine(appDataDir, "firebase_session.json");
            LoadSavedSession();
        }

        private void LoadSavedSession()
        {
            try
            {
                if (File.Exists(_sessionFilePath))
                {
                    string json = File.ReadAllText(_sessionFilePath);
                    _currentSession = JsonSerializer.Deserialize<FirebaseSession>(json);
                    if (_currentSession != null)
                    {
                        CurrentStatus = SyncStatus.Synced;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao carregar sessão Firebase: {ex.Message}");
            }
        }

        private void SaveSession()
        {
            try
            {
                if (_currentSession != null)
                {
                    string json = JsonSerializer.Serialize(_currentSession, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(_sessionFilePath, json);
                }
                else if (File.Exists(_sessionFilePath))
                {
                    File.Delete(_sessionFilePath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao salvar sessão Firebase: {ex.Message}");
            }
        }

        public async Task<bool> EnsureFreshTokenAsync()
        {
            if (_currentSession == null || string.IsNullOrWhiteSpace(_currentSession.RefreshToken))
                return false;

            if (!_currentSession.IsExpired)
                return true;

            try
            {
                string url = $"https://securetoken.googleapis.com/v1/token?key={ApiKey}";
                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "grant_type", "refresh_token" },
                    { "refresh_token", _currentSession.RefreshToken }
                });

                var response = await _httpClient.PostAsync(url, content);
                if (response.IsSuccessStatusCode)
                {
                    string respJson = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(respJson);
                    var root = doc.RootElement;

                    _currentSession.IdToken = root.GetProperty("id_token").GetString() ?? "";
                    _currentSession.RefreshToken = root.GetProperty("refresh_token").GetString() ?? _currentSession.RefreshToken;
                    int expiresIn = int.TryParse(root.GetProperty("expires_in").GetString(), out int exp) ? exp : 3600;
                    _currentSession.ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn);

                    SaveSession();
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao renovar token Firebase: {ex.Message}");
            }

            return false;
        }

        public async Task<(bool Success, string ErrorMessage)> SignInAsync(string email, string password)
        {
            try
            {
                string url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={ApiKey}";
                var payload = new
                {
                    email = email.Trim(),
                    password = password,
                    returnSecureToken = true
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, content);
                string respJson = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(respJson);
                    var root = doc.RootElement;

                    int expiresIn = int.TryParse(root.GetProperty("expiresIn").GetString(), out int exp) ? exp : 3600;

                    _currentSession = new FirebaseSession
                    {
                        LocalId = root.GetProperty("localId").GetString() ?? "",
                        Email = root.GetProperty("email").GetString() ?? email,
                        IdToken = root.GetProperty("idToken").GetString() ?? "",
                        RefreshToken = root.GetProperty("refreshToken").GetString() ?? "",
                        ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn)
                    };

                    SaveSession();
                    SetStatus(SyncStatus.Synced, "Conectado com sucesso");
                    return (true, string.Empty);
                }
                else
                {
                    string error = ParseFirebaseAuthError(respJson);
                    SetStatus(SyncStatus.Disconnected, error);
                    return (false, error);
                }
            }
            catch (Exception ex)
            {
                string msg = $"Erro de conexão: {ex.Message}";
                SetStatus(SyncStatus.Error, msg);
                return (false, msg);
            }
        }

        public async Task<(bool Success, string ErrorMessage)> SignUpAsync(string email, string password)
        {
            try
            {
                string url = $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={ApiKey}";
                var payload = new
                {
                    email = email.Trim(),
                    password = password,
                    returnSecureToken = true
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, content);
                string respJson = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(respJson);
                    var root = doc.RootElement;

                    int expiresIn = int.TryParse(root.GetProperty("expiresIn").GetString(), out int exp) ? exp : 3600;

                    _currentSession = new FirebaseSession
                    {
                        LocalId = root.GetProperty("localId").GetString() ?? "",
                        Email = root.GetProperty("email").GetString() ?? email,
                        IdToken = root.GetProperty("idToken").GetString() ?? "",
                        RefreshToken = root.GetProperty("refreshToken").GetString() ?? "",
                        ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn)
                    };

                    SaveSession();
                    SetStatus(SyncStatus.Synced, "Conta criada com sucesso");
                    return (true, string.Empty);
                }
                else
                {
                    string error = ParseFirebaseAuthError(respJson);
                    SetStatus(SyncStatus.Disconnected, error);
                    return (false, error);
                }
            }
            catch (Exception ex)
            {
                string msg = $"Erro de conexão: {ex.Message}";
                SetStatus(SyncStatus.Error, msg);
                return (false, msg);
            }
        }

        public void SignOut()
        {
            _currentSession = null;
            SaveSession();
            SetStatus(SyncStatus.Disconnected, "Desconectado");
        }

        // ==========================================
        // OPERAÇÕES DO CLOUD FIRESTORE
        // ==========================================

        public async Task<bool> UploadDocumentAsync(string collection, string documentId, string jsonContent, DateTime updatedAtUtc)
        {
            if (!await EnsureFreshTokenAsync() || _currentSession == null)
                return false;

            try
            {
                string url = $"https://firestore.googleapis.com/v1/projects/{ProjectId}/databases/(default)/documents/users/{_currentSession.LocalId}/{collection}/{documentId}?updateMask.fieldPaths=jsonContent&updateMask.fieldPaths=updatedAt&key={ApiKey}";

                var payload = new
                {
                    fields = new
                    {
                        jsonContent = new { stringValue = jsonContent },
                        updatedAt = new { stringValue = updatedAtUtc.ToString("o") }
                    }
                };

                var request = new HttpRequestMessage(HttpMethod.Patch, url)
                {
                    Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _currentSession.IdToken);

                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao fazer upload para Firestore: {ex.Message}");
                return false;
            }
        }

        public async Task<(string? JsonContent, DateTime? UpdatedAt)> DownloadDocumentAsync(string collection, string documentId)
        {
            if (!await EnsureFreshTokenAsync() || _currentSession == null)
                return (null, null);

            try
            {
                string url = $"https://firestore.googleapis.com/v1/projects/{ProjectId}/databases/(default)/documents/users/{_currentSession.LocalId}/{collection}/{documentId}?key={ApiKey}";

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _currentSession.IdToken);

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    string respJson = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(respJson);
                    var fields = doc.RootElement.GetProperty("fields");

                    string? jsonContent = null;
                    if (fields.TryGetProperty("jsonContent", out var jProp) && jProp.TryGetProperty("stringValue", out var sVal))
                    {
                        jsonContent = sVal.GetString();
                    }

                    DateTime? updatedAt = null;
                    if (fields.TryGetProperty("updatedAt", out var uProp) && uProp.TryGetProperty("stringValue", out var uVal))
                    {
                        if (DateTime.TryParse(uVal.GetString(), out var dt))
                            updatedAt = dt.ToUniversalTime();
                    }

                    return (jsonContent, updatedAt);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao baixar documento do Firestore: {ex.Message}");
            }

            return (null, null);
        }

        public async Task<List<(string DocumentId, string JsonContent, DateTime UpdatedAt)>> ListDocumentsAsync(string collection)
        {
            var results = new List<(string, string, DateTime)>();
            if (!await EnsureFreshTokenAsync() || _currentSession == null)
                return results;

            try
            {
                string url = $"https://firestore.googleapis.com/v1/projects/{ProjectId}/databases/(default)/documents/users/{_currentSession.LocalId}/{collection}?key={ApiKey}";

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _currentSession.IdToken);

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    string respJson = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(respJson);
                    if (doc.RootElement.TryGetProperty("documents", out var docsArray) && docsArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var d in docsArray.EnumerateArray())
                        {
                            string docName = d.GetProperty("name").GetString() ?? "";
                            string docId = Path.GetFileName(docName);

                            if (d.TryGetProperty("fields", out var fields))
                            {
                                string content = "";
                                if (fields.TryGetProperty("jsonContent", out var jProp) && jProp.TryGetProperty("stringValue", out var sVal))
                                {
                                    content = sVal.GetString() ?? "";
                                }

                                DateTime dt = DateTime.MinValue;
                                if (fields.TryGetProperty("updatedAt", out var uProp) && uProp.TryGetProperty("stringValue", out var uVal))
                                {
                                    DateTime.TryParse(uVal.GetString(), out dt);
                                }

                                results.Add((docId, content, dt.ToUniversalTime()));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao listar documentos no Firestore: {ex.Message}");
            }

            return results;
        }

        public void SetStatus(SyncStatus status, string? message = null)
        {
            CurrentStatus = status;
            if (status == SyncStatus.Synced)
            {
                LastSyncTime = DateTime.Now;
            }
            SyncStatusChanged?.Invoke(status, message);
        }

        private static string ParseFirebaseAuthError(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("error", out var errorElement))
                {
                    string rawCode = errorElement.GetProperty("message").GetString() ?? "";

                    if (rawCode.Contains("EMAIL_EXISTS"))
                        return "Este e-mail já está cadastrado. Alterne para a aba 'Entrar'.";
                    if (rawCode.Contains("INVALID_LOGIN_CREDENTIALS") || rawCode.Contains("INVALID_PASSWORD") || rawCode.Contains("EMAIL_NOT_FOUND"))
                        return "E-mail ou senha incorretos.";
                    if (rawCode.Contains("WEAK_PASSWORD"))
                        return "A senha deve conter no mínimo 6 caracteres.";
                    if (rawCode.Contains("INVALID_EMAIL"))
                        return "Por favor, digite um endereço de e-mail válido.";
                    if (rawCode.Contains("TOO_MANY_ATTEMPTS_TRY_LATER"))
                        return "Muitas tentativas sem sucesso. Tente novamente em alguns minutos.";

                    return rawCode;
                }
            }
            catch
            {
            }

            return "Ocorreu um erro ao comunicar com os servidores do Firebase.";
        }
    }
}
