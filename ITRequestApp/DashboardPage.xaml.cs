using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;

namespace ITRequestApp
{
    public partial class DashboardPage : Page
    {
        private readonly MainWindow _mainWindow;
        private readonly DatabaseManager _dbManager;
        private readonly User _currentUser;
        private string _attachedFilePath;
        private bool _isEditing;
        private DispatcherTimer _requestCheckTimer;
        private int _lastRequestCount;

        public DashboardPage(MainWindow mainWindow, User currentUser)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            _dbManager = new DatabaseManager();
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            DataContext = this;
            LoadFilterUsers();
            LoadRequests();
            UpdateButtonVisibility();
            _requestCheckTimer = new DispatcherTimer();
            _requestCheckTimer.Interval = TimeSpan.FromSeconds(10);
            _requestCheckTimer.Tick += RequestCheckTimer_Tick;
            _lastRequestCount = _dbManager.GetRequests(userId: _currentUser.Role == "Admin" ? (int?)null : _currentUser.Id).Count;
            _requestCheckTimer.Start();
        }

        public User CurrentUser => _currentUser;

        private void RequestCheckTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                var currentRequests = _dbManager.GetRequests(userId: _currentUser.Role == "Admin" ? (int?)null : _currentUser.Id);
                if (currentRequests.Count > _lastRequestCount)
                {
                    MessageBox.Show($"Появилось {currentRequests.Count - _lastRequestCount} новых заявок!", "Уведомление", MessageBoxButton.OK, MessageBoxImage.Information);
                    _lastRequestCount = currentRequests.Count;
                    LoadRequests();
                }
            }
            catch (Exception ex)
            {
                Log($"Error in RequestCheckTimer_Tick: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
        }

        private void UpdateButtonVisibility()
        {
            StatusComboBox.IsEnabled = _currentUser.Role == "Admin";
            DeleteButton.IsEnabled = _currentUser.Role == "Admin";
            ViewAuditButton.IsEnabled = _currentUser.Role == "Admin";
            AssignButton.IsEnabled = _currentUser.Role == "Admin";
            EditButton.IsEnabled = true;
        }

        private void LoadFilterUsers()
        {
            if (_currentUser.Role != "Admin") return;
            List<User> users = _dbManager.GetUsers();
            var filterUsers = new List<User> { new User { Id = 0, Name = "Все" } };
            filterUsers.AddRange(users);
            UserFilterComboBox.ItemsSource = filterUsers;
            UserFilterComboBox.SelectedIndex = 0;
        }

        private void LoadRequests()
        {
            List<Request> requests = _dbManager.GetRequests(
                userId: _currentUser.Role == "Admin" ? (int?)null : _currentUser.Id
            );
            RequestsListView.ItemsSource = requests;
        }

        private void LoadComments(int requestId)
        {
            List<Comment> comments = _dbManager.GetComments(requestId);
            CommentsListView.ItemsSource = comments;
        }

        private void ReassignButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedRequest = (Request)RequestsListView.SelectedItem;
            if (selectedRequest == null)
            {
                MessageBox.Show("Пожалуйста, выберите заявку для переназначения.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!selectedRequest.AssignedAdminId.HasValue)
            {
                MessageBox.Show("Заявка не назначена ни одному администратору.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var admins = _dbManager.GetUsers().FindAll(u => u.Role == "Admin" && u.Id != selectedRequest.AssignedAdminId);
            if (admins.Count == 0)
            {
                MessageBox.Show("Нет доступных администраторов для переназначения.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var reassignWindow = new Window
            {
                Title = "Переназначить заявку",
                Width = 300,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this)
            };
            var stackPanel = new StackPanel { Margin = new Thickness(10) };
            var comboBox = new ComboBox { ItemsSource = admins, DisplayMemberPath = "Name" };
            var button = new Button { Content = "Переназначить", Margin = new Thickness(5), Padding = new Thickness(5) };
            button.Click += (s, args) =>
            {
                var selectedAdmin = (User)comboBox.SelectedItem;
                if (selectedAdmin != null)
                {
                    _dbManager.AssignAdminToRequest(selectedRequest.Id, selectedAdmin.Id);
                    _dbManager.AddAuditLog(new AuditLog
                    {
                        UserId = _currentUser.Id,
                        Action = "Переназначение заявки",
                        Details = $"Заявка #{selectedRequest.Id} переназначена с администратора {selectedRequest.AssignedAdminId} на {selectedAdmin.Id} пользователем {_currentUser.Name}",
                        Timestamp = DateTime.Now
                    });
                    MessageBox.Show("Заявка успешно переназначена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadRequests();
                    reassignWindow.Close();
                }
            };
            stackPanel.Children.Add(new TextBlock { Text = "Выберите нового администратора:", Margin = new Thickness(5) });
            stackPanel.Children.Add(comboBox);
            stackPanel.Children.Add(button);
            reassignWindow.Content = stackPanel;
            reassignWindow.ShowDialog();
        }

        private void AttachFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Все файлы (*.*)|*.*",
                Title = "Выберите файл для прикрепления"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;
                FileInfo fileInfo = new FileInfo(filePath);
                if (fileInfo.Length > 200 * 1024 * 1024) // 200 МБ
                {
                    MessageBox.Show("Файл слишком большой. Максимальный размер файла - 200 МБ.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                _attachedFilePath = filePath;
                FilePathTextBlock.Text = System.IO.Path.GetFileName(_attachedFilePath);
            }
        }

        private void SubmitRequest_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(DescriptionTextBox.Text))
            {
                MessageBox.Show("Пожалуйста, введите описание проблемы.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Request request;
            if (_isEditing)
            {
                var selectedRequest = (Request)RequestsListView.SelectedItem;
                request = new Request
                {
                    Id = selectedRequest.Id,
                    UserId = selectedRequest.UserId,
                    Description = DescriptionTextBox.Text,
                    Status = selectedRequest.Status,
                    FilePath = _attachedFilePath,
                    CreatedAt = selectedRequest.CreatedAt,
                    AssignedAdminId = selectedRequest.AssignedAdminId,
                    CabinetNumber = CabinetNumberTextBox.Text,
                    IsCompletedByUser = selectedRequest.IsCompletedByUser
                };
            }
            else
            {
                request = new Request
                {
                    UserId = _currentUser.Id,
                    Description = DescriptionTextBox.Text,
                    Status = "Open",
                    FilePath = _attachedFilePath,
                    CreatedAt = DateTime.Now,
                    CabinetNumber = CabinetNumberTextBox.Text,
                    IsCompletedByUser = false
                };
            }

            if (_isEditing)
            {
                _dbManager.UpdateRequest(request);
                _dbManager.AddAuditLog(new AuditLog
                {
                    UserId = _currentUser.Id,
                    Action = "Редактирование заявки",
                    Details = $"Заявка #{request.Id} отредактирована пользователем {_currentUser.Name}: {request.Description}",
                    Timestamp = DateTime.Now
                });
                MessageBox.Show("Заявка успешно отредактирована!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                _isEditing = false;
                SubmitButton.Content = "Отправить";
            }
            else
            {
                _dbManager.AddRequest(request);
                _dbManager.AddAuditLog(new AuditLog
                {
                    UserId = _currentUser.Id,
                    Action = "Создание заявки",
                    Details = $"Заявка создана пользователем {_currentUser.Name}: {request.Description}",
                    Timestamp = DateTime.Now
                });
                MessageBox.Show("Заявка успешно отправлена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            LoadRequests();
            DescriptionTextBox.Text = string.Empty;
            FilePathTextBlock.Text = "Нет файла";
            _attachedFilePath = null;
        }

        private void EditRequest_Click(object sender, RoutedEventArgs e)
        {
            var selectedRequest = (Request)RequestsListView.SelectedItem;
            if (selectedRequest == null)
            {
                MessageBox.Show("Пожалуйста, выберите заявку для редактирования.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (_currentUser.Role != "Admin" && _currentUser.Id != selectedRequest.UserId)
            {
                MessageBox.Show("Вы можете редактировать только свои заявки.", "Ошибка доступа", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            DescriptionTextBox.Text = selectedRequest.Description;
            CabinetNumberTextBox.Text = selectedRequest.CabinetNumber;
            _attachedFilePath = selectedRequest.FilePath;
            FilePathTextBlock.Text = string.IsNullOrEmpty(selectedRequest.FilePath) ? "Нет файла" : System.IO.Path.GetFileName(selectedRequest.FilePath);
            _isEditing = true;
            SubmitButton.Content = "Сохранить изменения";
        }

        private void DeleteRequest_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser.Role != "Admin")
            {
                MessageBox.Show("Только администраторы могут удалять заявки.", "Ошибка доступа", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var selectedRequest = (Request)RequestsListView.SelectedItem;
            if (selectedRequest == null)
            {
                MessageBox.Show("Пожалуйста, выберите заявку для удаления.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var result = MessageBox.Show($"Вы уверены, что хотите удалить заявку #{selectedRequest.Id}?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                _dbManager.DeleteRequest(selectedRequest.Id);
                _dbManager.AddAuditLog(new AuditLog
                {
                    UserId = _currentUser.Id,
                    Action = "Удаление заявки",
                    Details = $"Заявка #{selectedRequest.Id} удалена пользователем {_currentUser.Name}",
                    Timestamp = DateTime.Now
                });
                MessageBox.Show("Заявка успешно удалена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadRequests();
                CommentsListView.ItemsSource = null;
            }
        }

        private static readonly Dictionary<string, string> StatusMapping = new Dictionary<string, string>
        {
            { "Открыт", "Open" },
            { "Выполняется", "InProgress" },
            { "Закрыт", "Closed" }
        };

        private void StatusComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedRequest = (Request)RequestsListView.SelectedItem;
            if (selectedRequest == null) return;

            var selectedStatusRussian = (StatusComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
            if (string.IsNullOrEmpty(selectedStatusRussian) || selectedStatusRussian == selectedRequest.StatusRussian) return;

            if (StatusMapping.TryGetValue(selectedStatusRussian, out string selectedStatusEnglish))
            {
                _dbManager.UpdateRequestStatus(selectedRequest.Id, selectedStatusEnglish);
                _dbManager.AddAuditLog(new AuditLog
                {
                    UserId = _currentUser.Id,
                    Action = "Изменение статуса",
                    Details = $"Заявка #{selectedRequest.Id} изменена на статус '{selectedStatusEnglish}' пользователем {_currentUser.Name}",
                    Timestamp = DateTime.Now
                });
                MessageBox.Show($"Статус заявки изменен на '{selectedStatusRussian}'.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadRequests();
            }
        }

        private void AssignButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser.Role != "Admin")
            {
                MessageBox.Show("Только администраторы могут отозваться на заявки.", "Ошибка доступа", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var selectedRequest = (Request)RequestsListView.SelectedItem;
            if (selectedRequest == null)
            {
                MessageBox.Show("Пожалуйста, выберите заявку для назначения.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (selectedRequest.AssignedAdminId.HasValue)
            {
                MessageBox.Show("Заявка уже назначена другому администратору.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _dbManager.AssignAdminToRequest(selectedRequest.Id, _currentUser.Id);
            _dbManager.AddAuditLog(new AuditLog
            {
                UserId = _currentUser.Id,
                Action = "Назначение на заявку",
                Details = $"Администратор {_currentUser.Name} назначен на заявку #{selectedRequest.Id}",
                Timestamp = DateTime.Now
            });
            MessageBox.Show("Вы успешно назначены на заявку!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            LoadRequests();
        }

        private void ApplyFilter_Click(object sender, RoutedEventArgs e)
        {
            string selectedStatusRussian = (StatusFilterComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
            string statusFilter = null;
            if (selectedStatusRussian != "Все" && StatusMapping.TryGetValue(selectedStatusRussian, out string englishStatus))
            {
                statusFilter = englishStatus;
            }

            int? userIdFilter = _currentUser.Role == "Admin" && ((User)UserFilterComboBox.SelectedItem)?.Id != 0 ? ((User)UserFilterComboBox.SelectedItem)?.Id : (_currentUser.Role == "Admin" ? (int?)null : _currentUser.Id);

            List<Request> requests = _dbManager.GetRequests(statusFilter, userIdFilter);
            RequestsListView.ItemsSource = requests;
        }

        private void BackupDatabase_Click(object sender, RoutedEventArgs e)
        {
            _dbManager.BackupDatabase();
            _dbManager.AddAuditLog(new AuditLog
            {
                UserId = _currentUser.Id,
                Action = "Резервное копирование",
                Details = "Создана резервная копия базы данных",
                Timestamp = DateTime.Now
            });
            MessageBox.Show("Резервная копия успешно создана!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ViewAudit_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser.Role != "Admin")
            {
                MessageBox.Show("Только администраторы могут просматривать аудит.", "Ошибка доступа", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var logs = _dbManager.GetAuditLogs();
            var logWindow = new Window
            {
                Title = "Логи аудита",
                Width = 600,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this)
            };
            var listView = new ListView { Margin = new Thickness(10) };
            var gridView = new GridView();
            gridView.Columns.Add(new GridViewColumn { Header = "ID", DisplayMemberBinding = new System.Windows.Data.Binding("Id"), Width = 50 });
            gridView.Columns.Add(new GridViewColumn { Header = "Пользователь", DisplayMemberBinding = new System.Windows.Data.Binding("UserId"), Width = 100 });
            gridView.Columns.Add(new GridViewColumn { Header = "Действие", DisplayMemberBinding = new System.Windows.Data.Binding("Action"), Width = 150 });
            gridView.Columns.Add(new GridViewColumn { Header = "Подробности", DisplayMemberBinding = new System.Windows.Data.Binding("Details"), Width = 200 });
            gridView.Columns.Add(new GridViewColumn { Header = "Время", DisplayMemberBinding = new System.Windows.Data.Binding("Timestamp"), Width = 100 });
            listView.View = gridView;
            listView.ItemsSource = logs;
            logWindow.Content = listView;
            logWindow.ShowDialog();
        }

        private void ManageUsers_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow.NavigateToUserManagement();
        }

        private void ViewFile_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null || string.IsNullOrEmpty(button.Tag?.ToString()))
            {
                MessageBox.Show("Файл не прикреплен к этой заявке.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string filePath = button.Tag.ToString();
            if (!File.Exists(filePath))
            {
                MessageBox.Show($"Файл не найден: {filePath}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
            _dbManager.AddAuditLog(new AuditLog
            {
                UserId = _currentUser.Id,
                Action = "Просмотр файла",
                Details = $"Файл {filePath} просмотрен пользователем {_currentUser.Name}",
                Timestamp = DateTime.Now
            });
        }

        private void AddComment_Click(object sender, RoutedEventArgs e)
        {
            var selectedRequest = (Request)RequestsListView.SelectedItem;
            if (selectedRequest == null || string.IsNullOrWhiteSpace(CommentTextBox.Text))
            {
                MessageBox.Show("Пожалуйста, выберите заявку и введите комментарий.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var comment = new Comment
            {
                RequestId = selectedRequest.Id,
                UserId = _currentUser.Id,
                Text = CommentTextBox.Text,
                CreatedAt = DateTime.Now
            };

            _dbManager.AddComment(comment);
            _dbManager.AddAuditLog(new AuditLog
            {
                UserId = _currentUser.Id,
                Action = "Добавление комментария",
                Details = $"Комментарий добавлен к заявке #{selectedRequest.Id} пользователем {_currentUser.Name}: {comment.Text}",
                Timestamp = DateTime.Now
            });
            MessageBox.Show("Комментарий успешно добавлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            CommentTextBox.Text = string.Empty;
            LoadComments(selectedRequest.Id);
        }

        private void CompleteRequestButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedRequest = (Request)RequestsListView.SelectedItem;
            if (selectedRequest == null || selectedRequest.UserId != _currentUser.Id || selectedRequest.IsCompletedByUser || !selectedRequest.AssignedAdminId.HasValue)
            {
                MessageBox.Show("Невозможно подтвердить заявку.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var reviewWindow = new Window
            {
                Title = "Подтверждение выполнения",
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this)
            };
            var stackPanel = new StackPanel { Margin = new Thickness(10) };
            var textBox = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 100, Margin = new Thickness(5) };
            var button = new Button { Content = "Подтвердить", Margin = new Thickness(5), Padding = new Thickness(5) };
            button.Click += (s, args) =>
            {
                _dbManager.UpdateRequestCompletion(selectedRequest.Id, true);
                if (!string.IsNullOrWhiteSpace(textBox.Text))
                {
                    _dbManager.AddReview(new Review
                    {
                        RequestId = selectedRequest.Id,
                        AdminId = selectedRequest.AssignedAdminId.Value,
                        Text = textBox.Text,
                        CreatedAt = DateTime.Now
                    });
                }
                _dbManager.AddAuditLog(new AuditLog
                {
                    UserId = _currentUser.Id,
                    Action = "Подтверждение выполнения",
                    Details = $"Заявка #{selectedRequest.Id} подтверждена пользователем {_currentUser.Name}",
                    Timestamp = DateTime.Now
                });
                MessageBox.Show("Заявка подтверждена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadRequests();
                reviewWindow.Close();
            };
            stackPanel.Children.Add(new Label { Content = "Оставьте отзыв (необязательно):" });
            stackPanel.Children.Add(textBox);
            stackPanel.Children.Add(button);
            reviewWindow.Content = stackPanel;
            reviewWindow.ShowDialog();
        }

        private void ViewAchievementsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser.Role != "Admin") return;

            var stats = _dbManager.GetAdminStatistics();
            var admins = _dbManager.GetUsers().Where(u => u.Role == "Admin").ToList();
            var achievementsWindow = new Window
            {
                Title = "Достижения администраторов",
                Width = 600,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this)
            };
            var listView = new ListView { Margin = new Thickness(10) };
            var gridView = new GridView();
            gridView.Columns.Add(new GridViewColumn { Header = "Администратор", DisplayMemberBinding = new System.Windows.Data.Binding("AdminName"), Width = 150 });
            gridView.Columns.Add(new GridViewColumn { Header = "За неделю", DisplayMemberBinding = new System.Windows.Data.Binding("Week"), Width = 100 });
            gridView.Columns.Add(new GridViewColumn { Header = "За месяц", DisplayMemberBinding = new System.Windows.Data.Binding("Month"), Width = 100 });
            gridView.Columns.Add(new GridViewColumn { Header = "За год", DisplayMemberBinding = new System.Windows.Data.Binding("Year"), Width = 100 });
            listView.View = gridView;
            var data = admins.Select(a => new
            {
                AdminName = a.Name,
                Week = stats.TryGetValue(a.Id, out var s) ? s.Week : 0,
                Month = stats.TryGetValue(a.Id, out s) ? s.Month : 0,
                Year = stats.TryGetValue(a.Id, out s) ? s.Year : 0
            }).ToList();
            listView.ItemsSource = data;
            achievementsWindow.Content = listView;
            achievementsWindow.ShowDialog();
        }

        private void RequestsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedRequest = (Request)RequestsListView.SelectedItem;
            if (selectedRequest != null)
            {
                LoadComments(selectedRequest.Id);
                StatusComboBox.SelectedItem = StatusComboBox.Items.Cast<ComboBoxItem>().FirstOrDefault(i => i.Content.ToString() == selectedRequest.StatusRussian);
            }
            else
            {
                CommentsListView.ItemsSource = null;
                StatusComboBox.SelectedIndex = -1;
            }
            UpdateButtonVisibility();
        }

        private void Log(string message)
        {
            try
            {
                string logPath = "app_log.txt";
                string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: {message}\n";
                File.AppendAllText(logPath, logMessage);
            }
            catch
            {
                // Игнорируем ошибки логирования
            }
        }
    }
}