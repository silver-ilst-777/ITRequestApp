using System;
using System.Windows;
using System.Windows.Controls;

namespace ITRequestApp
{
    public partial class LoginPage : Page
    {
        private readonly MainWindow _mainWindow;
        private readonly DatabaseManager _dbManager;

        public LoginPage(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            _dbManager = new DatabaseManager();
        }

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow.NavigateToRegistration();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string username = UsernameTextBox.Text;
                string password = PasswordBox.Password;

                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    MessageBox.Show("Пожалуйста, введите имя пользователя и пароль.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var users = _dbManager.GetUsers();
                var user = users.Find(u => u.Name == username && u.Password == DatabaseManager.HashPassword(password));

                if (user != null)
                {
                    _mainWindow.NavigateToDashboard(user);
                }
                else
                {
                    MessageBox.Show("Неверное имя пользователя или пароль.", "Ошибка входа", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при входе: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}