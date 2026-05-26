using System.Windows;
using System.Windows.Input;

namespace OOPWPFProject
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            // Enter для входу
            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                    LoginButton_Click(s, null);
            };
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string login = LoginBox.Text.Trim();
            string password = PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
            {
                ErrorBlock.Text = "Введіть логін і пароль.";
            
            }
            else
            {
                
            

                var (success, role) = DatabaseManager.Login(login, password);

            if (success)
            {
                Logger.CurrentUser = login;
                Logger.Log("Вхід", $"Користувач '{login}' увійшов як {role}");

                var main = new MainWindow(role);
                main.Show();
                Close();
            }
            else
            {
                ErrorBlock.Text = "Невірний логін або пароль.";
                PasswordBox.Clear();
            }
        }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();

        }

        
    }
}