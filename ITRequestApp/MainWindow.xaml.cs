using System.Windows;

namespace ITRequestApp
{
    public partial class MainWindow : Window
    {
        private readonly DatabaseManager _dbManager;
        private User _currentUser;

        public MainWindow()
        {
            InitializeComponent();
            _dbManager = new DatabaseManager();
            NavigateToLogin();
        }

        public void NavigateToLogin()
        {
            MainFrame.Navigate(new LoginPage(this));
        }

        public void NavigateToDashboard(User user)
        {
            _currentUser = user;
            MainFrame.Navigate(new DashboardPage(this, _currentUser));
        }

        public void NavigateToRegistration()
        {
            MainFrame.Navigate(new RegistrationPage(this));
        }

        public void NavigateToUserManagement()
        {
            if (_currentUser?.Role == "Admin")
            {
                MainFrame.Navigate(new UserManagementPage(this, _currentUser));
            }
            else
            {
                MessageBox.Show("Только администраторы могут управлять пользователями.", "Ошибка доступа", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}