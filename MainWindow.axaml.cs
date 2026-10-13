using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using TodoListApp.Models;
using TodoListApp.Services;

namespace TodoListApp.Linux
{
    public partial class MainWindow : Window
    {
        private readonly TodoStorageService _storageService = new();
        private readonly FirebaseService _firebaseService;
        private readonly SyncCoordinator _syncCoordinator;
        private readonly ThemeService _themeService = new();

        private readonly DispatcherTimer _periodicSyncTimer;
        private ProfileStorageData _profileData = new();
        public ObservableCollection<UserProfile> Profiles { get; } = new();
        private UserProfile? _activeProfile;

        private readonly ObservableCollection<TodoItem> _allTodos = new();
        public ObservableCollection<TodoItem> FilteredTodos { get; } = new();

        private enum FilterMode { All, Pending, Completed }
        private FilterMode _currentFilter = FilterMode.All;
        private bool _isRegisterMode = false;

        public MainWindow()
        {
            InitializeComponent();

            _firebaseService = new FirebaseService();
            _syncCoordinator = new SyncCoordinator(_firebaseService, _storageService);

            // Inicializa sons
            SoundEffectHelper.Initialize();

            // Configura Data no cabeçalho
            var culture = new CultureInfo("pt-BR");
            string dateStr = DateTime.Now.ToString("dddd, d 'de' MMMM", culture);
            if (!string.IsNullOrEmpty(dateStr))
            {
                dateStr = char.ToUpper(dateStr[0]) + dateStr.Substring(1);
            }
            DateText.Text = dateStr;

            // Tema Salvo
            bool isDark = _storageService.LoadThemePreference();
            _themeService.ApplyTheme(isDark);
            _themeService.ThemeChanged += OnThemeChanged;
            UpdateThemeSwitchUi(isDark);

            // Perfis e Tarefas
            ProfilesItemsControl.ItemsSource = Profiles;
            TodoListControl.ItemsSource = FilteredTodos;

            LoadProfilesAndData();

            // Sincronização em Nuvem
            _firebaseService.SyncStatusChanged += (status, msg) =>
            {
                Dispatcher.UIThread.Post(UpdateCloudSyncUi);
            };

            _syncCoordinator.DataChangedFromCloud += () =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    LoadProfilesAndData();
                });
            };

            // Timer de sincronização periódica (30s)
            _periodicSyncTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _periodicSyncTimer.Tick += async (s, e) =>
            {
                if (_firebaseService.IsLoggedIn)
                {
                    await _syncCoordinator.SyncAllAsync();
                }
            };
            _periodicSyncTimer.Start();

            UpdateCloudSyncUi();
            UpdateFilterPillStyles();

            // Sincronização inicial se já estiver logado
            if (_firebaseService.IsLoggedIn)
            {
                Task.Run(async () =>
                {
                    await Task.Delay(1000);
                    await _syncCoordinator.SyncAllAsync();
                });
            }
        }

        // ==========================================
        // DADOS E PERFIS
        // ==========================================

        private void LoadProfilesAndData()
        {
            _profileData = _storageService.LoadProfileData();
            Profiles.Clear();

            foreach (var p in _profileData.Profiles)
            {
                Profiles.Add(p);
            }

            if (!string.IsNullOrWhiteSpace(_profileData.ActiveProfileId))
            {
                _activeProfile = Profiles.FirstOrDefault(p => p.Id == _profileData.ActiveProfileId);
            }

            if (_activeProfile == null && Profiles.Count > 0)
            {
                _activeProfile = Profiles[0];
                _profileData.ActiveProfileId = _activeProfile.Id;
                _storageService.SaveProfileData(_profileData);
            }

            UpdateActiveProfileUi();
            LoadTodosForActiveProfile();
        }

        private void UpdateActiveProfileUi()
        {
            foreach (var p in Profiles)
            {
                p.IsActive = (_activeProfile != null && p.Id == _activeProfile.Id);
            }

            if (_activeProfile != null)
            {
                ProfileAvatarInitial.Text = _activeProfile.Initial;
                ProfileAvatarBorder.Background = this.FindResource("PrimaryBrush") as IBrush ?? Brushes.Indigo;
                ProfileAvatarBorder.BorderBrush = this.FindResource("PrimaryBrush") as IBrush ?? Brushes.Indigo;
                ProfileAvatarInitial.Foreground = Brushes.White;

                ProfileNameText.Text = _activeProfile.Name;
                ProfileNameText.Foreground = this.FindResource("TextPrimaryBrush") as IBrush ?? Brushes.Black;
            }
            else
            {
                ProfileAvatarInitial.Text = "+";
                ProfileAvatarBorder.Background = Brushes.Transparent;
                ProfileAvatarBorder.BorderBrush = this.FindResource("PrimaryBrush") as IBrush ?? Brushes.Indigo;
                ProfileAvatarInitial.Foreground = this.FindResource("PrimaryBrush") as IBrush ?? Brushes.Indigo;

                ProfileNameText.Text = "Nenhum perfil";
                ProfileNameText.Foreground = this.FindResource("TextMutedBrush") as IBrush ?? Brushes.Gray;
            }
        }

        private void LoadTodosForActiveProfile()
        {
            string? profileId = _activeProfile?.Id;
            var list = _storageService.LoadTodos(profileId);

            _allTodos.Clear();
            foreach (var item in list)
            {
                _allTodos.Add(item);
            }

            ApplyFilter();
        }

        private void SaveCurrentTodos()
        {
            string? profileId = _activeProfile?.Id;
            _storageService.SaveTodos(_allTodos, profileId);
            _syncCoordinator.ScheduleDebouncedSync();
            UpdateCounters();
        }

        private void ApplyFilter()
        {
            FilteredTodos.Clear();
            var query = _currentFilter switch
            {
                FilterMode.Pending => _allTodos.Where(t => !t.IsCompleted),
                FilterMode.Completed => _allTodos.Where(t => t.IsCompleted),
                _ => _allTodos
            };

            foreach (var t in query)
            {
                FilteredTodos.Add(t);
            }

            EmptyStatePanel.IsVisible = FilteredTodos.Count == 0;
            UpdateCounters();
        }

        private void UpdateCounters()
        {
            int total = _allTodos.Count;
            int completed = _allTodos.Count(t => t.IsCompleted);
            int pending = total - completed;

            if (total == 0)
            {
                SummaryText.Text = "Nenhuma tarefa";
            }
            else
            {
                int pct = (completed * 100) / total;
                SummaryText.Text = $"{completed} de {total} concluídas ({pct}%)";
            }

            RemainingCounterText.Text = pending == 1 ? "1 tarefa restante" : $"{pending} tarefas restantes";
        }

        // ==========================================
        // TAREFAS
        // ==========================================

        private void AddTaskButton_Click(object? sender, RoutedEventArgs e)
        {
            CommitNewTask();
        }

        private void NewTaskTextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitNewTask();
            }
        }

        private void CommitNewTask()
        {
            string text = NewTaskTextBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text)) return;

            var newItem = new TodoItem
            {
                Id = Guid.NewGuid(),
                Title = text,
                IsCompleted = false,
                CreatedAt = DateTime.Now
            };

            _allTodos.Insert(0, newItem);
            NewTaskTextBox.Text = string.Empty;

            SaveCurrentTodos();
            ApplyFilter();
        }

        private void TodoCheckBox_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.Tag is TodoItem todo)
            {
                bool wasCompleted = cb.IsChecked == true;
                todo.IsCompleted = wasCompleted;

                if (wasCompleted)
                {
                    SoundEffectHelper.PlayTaskCompleted();
                    ConfettiCanvas.LaunchMainTaskConfetti();

                    // Move para o final da lista
                    _allTodos.Remove(todo);
                    _allTodos.Add(todo);
                }

                SaveCurrentTodos();
                ApplyFilter();
            }
        }

        private void DeleteButton_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is TodoItem todo)
            {
                _allTodos.Remove(todo);
                SaveCurrentTodos();
                ApplyFilter();
            }
        }

        private void ToggleSubtasksButton_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is TodoItem todo)
            {
                todo.IsExpanded = !todo.IsExpanded;
            }
        }

        private void AddSubTaskButton_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is TodoItem todo)
            {
                CommitSubTask(todo);
            }
        }

        private void NewSubtaskTextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox tb && tb.Tag is TodoItem todo)
            {
                CommitSubTask(todo, tb);
            }
        }

        private void CommitSubTask(TodoItem todo, TextBox? targetBox = null)
        {
            TextBox? tb = targetBox;
            if (tb == null)
            {
                // Tenta localizar o TextBox de subtarefa
                tb = this.FindControl<TextBox>("NewSubtaskTextBox");
            }

            string text = tb?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text)) return;

            var subtask = new SubTaskItem
            {
                Id = Guid.NewGuid(),
                Title = text,
                IsCompleted = false
            };

            todo.SubTasks.Add(subtask);
            todo.UpdateSubtasksSummary();

            if (tb != null) tb.Text = string.Empty;

            SaveCurrentTodos();
        }

        private void SubTaskCheckBox_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.Tag is SubTaskItem subtask)
            {
                bool isDone = cb.IsChecked == true;
                subtask.IsCompleted = isDone;

                // Localiza o Todo pai
                var parentTodo = _allTodos.FirstOrDefault(t => t.SubTasks.Contains(subtask));

                if (isDone)
                {
                    SoundEffectHelper.PlaySubtaskChime();

                    // Dispara fogos radiais centrados no checkbox
                    var center = cb.TranslatePoint(new Point(cb.Bounds.Width / 2, cb.Bounds.Height / 2), ConfettiCanvas);
                    if (center.HasValue)
                    {
                        ConfettiCanvas.LaunchSubtaskFireworks(center.Value);
                    }
                }

                if (parentTodo != null)
                {
                    parentTodo.UpdateSubtasksSummary();

                    // Se todas as subtarefas foram concluídas, conclui a tarefa pai
                    if (isDone && parentTodo.AllSubtasksCompleted && !parentTodo.IsCompleted)
                    {
                        parentTodo.IsCompleted = true;
                        SoundEffectHelper.PlayTaskCompleted();
                        ConfettiCanvas.LaunchMainTaskConfetti();

                        _allTodos.Remove(parentTodo);
                        _allTodos.Add(parentTodo);
                        ApplyFilter();
                    }
                }

                SaveCurrentTodos();
            }
        }

        private void DeleteSubTaskButton_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is SubTaskItem subtask)
            {
                var parentTodo = _allTodos.FirstOrDefault(t => t.SubTasks.Contains(subtask));
                if (parentTodo != null)
                {
                    parentTodo.SubTasks.Remove(subtask);
                    parentTodo.UpdateSubtasksSummary();
                    SaveCurrentTodos();
                }
            }
        }

        private void ClearCompletedButton_Click(object? sender, RoutedEventArgs e)
        {
            var completed = _allTodos.Where(t => t.IsCompleted).ToList();
            if (completed.Count == 0) return;

            foreach (var item in completed)
            {
                _allTodos.Remove(item);
            }

            SaveCurrentTodos();
            ApplyFilter();
        }

        // ==========================================
        // FILTROS
        // ==========================================

        private void FilterAllButton_Click(object? sender, RoutedEventArgs e)
        {
            _currentFilter = FilterMode.All;
            UpdateFilterPillStyles();
            ApplyFilter();
        }

        private void FilterPendingButton_Click(object? sender, RoutedEventArgs e)
        {
            _currentFilter = FilterMode.Pending;
            UpdateFilterPillStyles();
            ApplyFilter();
        }

        private void FilterCompletedButton_Click(object? sender, RoutedEventArgs e)
        {
            _currentFilter = FilterMode.Completed;
            UpdateFilterPillStyles();
            ApplyFilter();
        }

        private void UpdateFilterPillStyles()
        {
            var activeBg = this.FindResource("PrimaryBrush") as IBrush ?? Brushes.Indigo;
            var inactiveBg = this.FindResource("SubSurfaceBrush") as IBrush ?? Brushes.LightGray;

            var activeFg = Brushes.White;
            var inactiveFg = this.FindResource("TextSubtitleBrush") as IBrush ?? Brushes.Gray;

            FilterAllButton.Background = _currentFilter == FilterMode.All ? activeBg : inactiveBg;
            FilterAllButton.Foreground = _currentFilter == FilterMode.All ? activeFg : inactiveFg;

            FilterPendingButton.Background = _currentFilter == FilterMode.Pending ? activeBg : inactiveBg;
            FilterPendingButton.Foreground = _currentFilter == FilterMode.Pending ? activeFg : inactiveFg;

            FilterCompletedButton.Background = _currentFilter == FilterMode.Completed ? activeBg : inactiveBg;
            FilterCompletedButton.Foreground = _currentFilter == FilterMode.Completed ? activeFg : inactiveFg;
        }

        // ==========================================
        // TEMA (CLARO / ESCURO)
        // ==========================================

        private void ThemeToggleButton_Click(object? sender, RoutedEventArgs e)
        {
            bool nextDark = !_themeService.IsDarkMode;
            _themeService.ApplyTheme(nextDark);
            _storageService.SaveThemePreference(nextDark);
        }

        private void OnThemeChanged(bool isDark)
        {
            UpdateThemeSwitchUi(isDark);
            UpdateActiveProfileUi();
            UpdateCloudSyncUi();
            UpdateFilterPillStyles();
        }

        private void UpdateThemeSwitchUi(bool isDark)
        {
            Canvas.SetLeft(SwitchThumb, isDark ? 16 : 2);

            SunIconPath.Stroke = isDark ? (this.FindResource("TextMutedBrush") as IBrush ?? Brushes.Gray) : Brush.Parse("#F59E0B");
            SunIconPath.Fill = isDark ? Brushes.Transparent : Brush.Parse("#33F59E0B");

            MoonIconPath.Stroke = isDark ? Brush.Parse("#818CF8") : (this.FindResource("TextMutedBrush") as IBrush ?? Brushes.Gray);
            MoonIconPath.Fill = isDark ? Brush.Parse("#33818CF8") : Brushes.Transparent;
        }

        // ==========================================
        // MODAIS E PERFIS
        // ==========================================

        private void ProfileButton_Click(object? sender, RoutedEventArgs e)
        {
            ProfileModal.IsVisible = true;
        }

        private void ProfileModalBackdrop_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            ProfileModal.IsVisible = false;
        }

        private void CloseProfileModalButton_Click(object? sender, RoutedEventArgs e)
        {
            ProfileModal.IsVisible = false;
        }

        private void ProfileItem_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is UserProfile profile)
            {
                _activeProfile = profile;
                _profileData.ActiveProfileId = profile.Id;
                _storageService.SaveProfileData(_profileData);

                UpdateActiveProfileUi();
                LoadTodosForActiveProfile();
                ProfileModal.IsVisible = false;

                _syncCoordinator.ScheduleDebouncedSync();
            }
        }

        private void DeleteProfileButton_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is UserProfile profile)
            {
                _profileData.Profiles.RemoveAll(p => p.Id == profile.Id);
                _storageService.DeleteProfileData(profile.Id);
                Profiles.Remove(profile);

                if (_activeProfile?.Id == profile.Id)
                {
                    _activeProfile = Profiles.FirstOrDefault();
                    _profileData.ActiveProfileId = _activeProfile?.Id;
                }

                _storageService.SaveProfileData(_profileData);
                UpdateActiveProfileUi();
                LoadTodosForActiveProfile();

                _syncCoordinator.ScheduleDebouncedSync();
            }
        }

        private void OpenCreateProfileModalButton_Click(object? sender, RoutedEventArgs e)
        {
            ProfileModal.IsVisible = false;
            NewProfileNameTextBox.Text = string.Empty;
            CreateProfileModal.IsVisible = true;
            NewProfileNameTextBox.Focus();
        }

        private void CreateProfileModalBackdrop_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            CreateProfileModal.IsVisible = false;
        }

        private void CancelCreateProfileButton_Click(object? sender, RoutedEventArgs e)
        {
            CreateProfileModal.IsVisible = false;
        }

        private void NewProfileNameTextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ConfirmCreateProfileButton_Click(sender, e);
            }
        }

        private void ConfirmCreateProfileButton_Click(object? sender, RoutedEventArgs e)
        {
            string name = NewProfileNameTextBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name)) return;

            var newProfile = new UserProfile
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                CreatedAt = DateTime.Now
            };

            _profileData.Profiles.Add(newProfile);
            _profileData.ActiveProfileId = newProfile.Id;
            _storageService.SaveProfileData(_profileData);

            Profiles.Add(newProfile);
            _activeProfile = newProfile;

            UpdateActiveProfileUi();
            LoadTodosForActiveProfile();

            CreateProfileModal.IsVisible = false;
            _syncCoordinator.ScheduleDebouncedSync();
        }

        // ==========================================
        // NUVEM (FIREBASE REST)
        // ==========================================

        private void CloudSyncButton_Click(object? sender, RoutedEventArgs e)
        {
            CloudSyncModal.IsVisible = true;
            UpdateCloudSyncUi();
        }

        private void CloudSyncModalBackdrop_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            CloudSyncModal.IsVisible = false;
        }

        private void CloseCloudSyncModalButton_Click(object? sender, RoutedEventArgs e)
        {
            CloudSyncModal.IsVisible = false;
        }

        private void CloudTabLoginButton_Click(object? sender, RoutedEventArgs e)
        {
            _isRegisterMode = false;
            UpdateCloudAuthTabs();
        }

        private void CloudTabRegisterButton_Click(object? sender, RoutedEventArgs e)
        {
            _isRegisterMode = true;
            UpdateCloudAuthTabs();
        }

        private void UpdateCloudAuthTabs()
        {
            var activeBg = this.FindResource("PrimaryBrush") as IBrush ?? Brushes.Indigo;
            var inactiveBg = this.FindResource("SubSurfaceBrush") as IBrush ?? Brushes.LightGray;

            CloudTabLoginButton.Background = !_isRegisterMode ? activeBg : inactiveBg;
            CloudTabLoginButton.Foreground = !_isRegisterMode ? Brushes.White : (this.FindResource("TextSecondaryBrush") as IBrush ?? Brushes.Gray);

            CloudTabRegisterButton.Background = _isRegisterMode ? activeBg : inactiveBg;
            CloudTabRegisterButton.Foreground = _isRegisterMode ? Brushes.White : (this.FindResource("TextSecondaryBrush") as IBrush ?? Brushes.Gray);

            CloudAuthSubmitButton.Content = _isRegisterMode ? "Criar Conta" : "Entrar na Conta";
            CloudAuthErrorText.IsVisible = false;
        }

        private void CloudPasswordBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CloudAuthSubmitButton_Click(sender, e);
            }
        }

        private async void CloudAuthSubmitButton_Click(object? sender, RoutedEventArgs e)
        {
            string email = CloudEmailTextBox.Text?.Trim() ?? string.Empty;
            string pass = CloudPasswordBox.Text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(pass))
            {
                CloudAuthErrorText.Text = "Preencha o e-mail e a senha.";
                CloudAuthErrorText.IsVisible = true;
                return;
            }

            CloudAuthSubmitButton.IsEnabled = false;
            CloudAuthErrorText.IsVisible = false;

            try
            {
                bool success;
                string? error;

                if (_isRegisterMode)
                {
                    (success, error) = await _firebaseService.SignUpAsync(email, pass);
                }
                else
                {
                    (success, error) = await _firebaseService.SignInAsync(email, pass);
                }

                if (success)
                {
                    UpdateCloudSyncUi();
                    await _syncCoordinator.SyncAllAsync();
                    LoadProfilesAndData();
                }
                else
                {
                    CloudAuthErrorText.Text = error ?? "Ocorreu um erro na autenticação.";
                    CloudAuthErrorText.IsVisible = true;
                }
            }
            finally
            {
                CloudAuthSubmitButton.IsEnabled = true;
            }
        }

        private async void CloudManualSyncButton_Click(object? sender, RoutedEventArgs e)
        {
            CloudManualSyncButton.IsEnabled = false;
            try
            {
                await _syncCoordinator.SyncAllAsync();
                LoadProfilesAndData();
                UpdateCloudSyncUi();
            }
            finally
            {
                CloudManualSyncButton.IsEnabled = true;
            }
        }

        private void CloudSignOutButton_Click(object? sender, RoutedEventArgs e)
        {
            _firebaseService.SignOut();
            UpdateCloudSyncUi();
        }

        private void UpdateCloudSyncUi()
        {
            bool loggedIn = _firebaseService.IsLoggedIn;

            var primaryBrush = this.FindResource("PrimaryBrush") as IBrush ?? Brushes.Indigo;
            var mutedBrush = this.FindResource("TextMutedBrush") as IBrush ?? Brushes.Gray;

            CloudSyncIcon.Foreground = loggedIn ? primaryBrush : mutedBrush;
            CloudSyncText.Foreground = loggedIn ? primaryBrush : mutedBrush;
            CloudSyncText.Text = loggedIn ? "Sincronizado" : "Nuvem";

            CloudAuthPanel.IsVisible = !loggedIn;
            CloudConnectedPanel.IsVisible = loggedIn;

            if (loggedIn)
            {
                CloudUserEmailText.Text = _firebaseService.CurrentSession?.Email ?? "usuario@email.com";
                if (_firebaseService.LastSyncTime.HasValue)
                {
                    CloudLastSyncText.Text = $"Última sincronização: {_firebaseService.LastSyncTime.Value:dd/MM/yyyy HH:mm:ss}";
                }
                else
                {
                    CloudLastSyncText.Text = "Última sincronização: Agora mesmo";
                }
            }
            else
            {
                UpdateCloudAuthTabs();
            }
        }

        // ==========================================
        // CONTROLES DE JANELA (BARRA DE TÍTULO)
        // ==========================================

        private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                BeginMoveDrag(e);
            }
        }

        private void MinimizeButton_Click(object? sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object? sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void CloseButton_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}