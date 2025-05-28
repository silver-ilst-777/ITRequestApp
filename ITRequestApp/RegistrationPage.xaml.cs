using System;
using System.Windows;
using System.Windows.Controls;

namespace ITRequestApp
{
    public partial class RegistrationPage : Page
    {
        private readonly MainWindow _mainWindow;
        private readonly DatabaseManager _dbManager;

        public RegistrationPage(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            _dbManager = new DatabaseManager();
            LoadDepartments();
        }

        private void LoadDepartments()
        {
            try
            {
                var departments = _dbManager.GetDepartments();
                DepartmentComboBox.ItemsSource = departments;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке отделов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string username = UsernameTextBox.Text;
                string password = PasswordBox.Password;
                var department = (Department)DepartmentComboBox.SelectedItem;

                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || department == null)
                {
                    MessageBox.Show("Пожалуйста, заполните все поля.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var existingUsers = _dbManager.GetUsers();
                if (existingUsers.Exists(u => u.Name.Equals(username, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show("Пользователь с таким именем уже существует.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var newUser = new User
                {
                    Name = username,
                    Password = DatabaseManager.HashPassword(password),
                    DepartmentId = department.Id,
                    Role = "User"
                };

                _dbManager.AddUser(newUser);
                _dbManager.AddAuditLog(new AuditLog
                {
                    UserId = 0, // Системное действие
                    Action = "Регистрация пользователя",
                    Details = $"Новый пользователь зарегистрирован: {username}",
                    Timestamp = DateTime.Now
                });

                MessageBox.Show("Регистрация успешна! Теперь вы можете войти.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                _mainWindow.NavigateToLogin();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при регистрации: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow.NavigateToLogin();
        }
    }
}