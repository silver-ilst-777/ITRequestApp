using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ITRequestApp
{
    public partial class UserManagementPage : Page
    {
        private readonly MainWindow _mainWindow;
        private readonly DatabaseManager _dbManager;
        private readonly User _currentUser;

        public UserManagementPage(MainWindow mainWindow, User currentUser)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            _dbManager = new DatabaseManager();
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            LoadDepartments();
            LoadUsers();
        }

        private void LoadDepartments()
        {
            var departments = _dbManager.GetDepartments();
            DepartmentComboBox.ItemsSource = departments;
        }

        private void LoadUsers()
        {
            var users = _dbManager.GetUsers();
            var departments = _dbManager.GetDepartments();
            var userDisplayList = users.Select(u => new
            {
                u.Id,
                u.Name,
                DepartmentName = departments.FirstOrDefault(d => d.Id == u.DepartmentId)?.Name ?? "Неизвестно",
                u.Role
            }).ToList();
            UsersListView.ItemsSource = userDisplayList;
        }

        private void CreateUserButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser.Role != "Admin")
            {
                MessageBox.Show("Только администраторы могут создавать пользователей.", "Ошибка доступа", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string username = UsernameTextBox.Text;
            string password = PasswordBox.Password;
            var department = (Department)DepartmentComboBox.SelectedItem;
            string role = (RoleComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || department == null || string.IsNullOrWhiteSpace(role))
            {
                MessageBox.Show("Пожалуйста, заполните все поля.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var newUser = new User
            {
                Name = username,
                Password = DatabaseManager.HashPassword(password),
                DepartmentId = department.Id,
                Role = role
            };

            _dbManager.AddUser(newUser);
            MessageBox.Show("Пользователь успешно создан!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            UsernameTextBox.Text = string.Empty;
            PasswordBox.Password = string.Empty;
            DepartmentComboBox.SelectedIndex = -1;
            RoleComboBox.SelectedIndex = -1;
            LoadUsers();
        }

        private void DeleteUserButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser.Role != "Admin")
            {
                MessageBox.Show("Только администраторы могут удалять пользователей.", "Ошибка доступа", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var button = sender as Button;
            if (button == null || !(button.Tag is int userId)) return;

            if (userId == _currentUser.Id)
            {
                MessageBox.Show("Нельзя удалить текущего пользователя.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var result = MessageBox.Show($"Вы уверены, что хотите удалить пользователя с ID {userId}?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                _dbManager.DeleteUser(userId);
                MessageBox.Show("Пользователь успешно удален!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadUsers();
            }
        }

        private void ChangeUsernameButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser.Role != "Admin") return;

            var button = sender as Button;
            if (button == null || !(button.Tag is int userId)) return;

            var inputDialog = new Window
            {
                Title = "Изменить логин",
                Width = 300,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this)
            };
            var stackPanel = new StackPanel { Margin = new Thickness(10) };
            var textBox = new TextBox { Margin = new Thickness(5) };
            var okButton = new Button { Content = "ОК", Margin = new Thickness(5), Padding = new Thickness(5) };
            okButton.Click += (s, args) =>
            {
                if (!string.IsNullOrWhiteSpace(textBox.Text))
                {
                    _dbManager.UpdateUsername(userId, textBox.Text);
                    MessageBox.Show("Логин успешно изменен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadUsers();
                    inputDialog.Close();
                }
            };
            stackPanel.Children.Add(new Label { Content = "Новый логин:" });
            stackPanel.Children.Add(textBox);
            stackPanel.Children.Add(okButton);
            inputDialog.Content = stackPanel;
            inputDialog.ShowDialog();
        }

        private void ChangePasswordButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser.Role != "Admin") return;

            var button = sender as Button;
            if (button == null || !(button.Tag is int userId)) return;

            var inputDialog = new Window
            {
                Title = "Изменить пароль",
                Width = 300,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this)
            };
            var stackPanel = new StackPanel { Margin = new Thickness(10) };
            var passwordBox = new PasswordBox { Margin = new Thickness(5) };
            var okButton = new Button { Content = "ОК", Margin = new Thickness(5), Padding = new Thickness(5) };
            okButton.Click += (s, args) =>
            {
                if (!string.IsNullOrWhiteSpace(passwordBox.Password))
                {
                    _dbManager.UpdatePassword(userId, DatabaseManager.HashPassword(passwordBox.Password));
                    MessageBox.Show("Пароль успешно изменен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadUsers();
                    inputDialog.Close();
                }
            };
            stackPanel.Children.Add(new Label { Content = "Новый пароль:" });
            stackPanel.Children.Add(passwordBox);
            stackPanel.Children.Add(okButton);
            inputDialog.Content = stackPanel;
            inputDialog.ShowDialog();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow.NavigateToDashboard(_currentUser);
        }
    }
}