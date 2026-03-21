using System.Windows;
using System.Windows.Controls;

namespace ScreenRecApp
{
    public partial class PromptWindow : Window
    {
        public string ResultName { get; private set; }
        public string ResultTimestamp { get; private set; }

        public PromptWindow()
        {
            InitializeComponent();
            TimestampBox.Text = System.DateTime.Now.ToString("dd.MM.yy_HH.mm.ss_");
            LoadSuggestions();
            Loaded += (s, e) =>
            {
                // Focus the name field so the user can start typing immediately
                NameComboBox.Focus();
                var textBox = NameComboBox.Template?.FindName("PART_EditableTextBox", NameComboBox) as System.Windows.Controls.TextBox;
                textBox?.SelectAll();
            };
        }

        private void LoadSuggestions()
        {
            NameComboBox.ItemsSource = null;
            NameComboBox.ItemsSource = SettingsManager.Settings.History;
        }

        private void DeleteSuggestion_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string suggestion)
            {
                SettingsManager.Settings.History.Remove(suggestion);
                SettingsManager.Save();
                LoadSuggestions();
                e.Handled = true;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            ResultName = NameComboBox.Text?.Trim();
            ResultTimestamp = TimestampBox.Text?.Trim();

            if (string.IsNullOrEmpty(ResultName))
            {
                ResultName = "untitled";
            }
            else
            {
                if (!SettingsManager.Settings.History.Contains(ResultName))
                {
                    SettingsManager.Settings.History.Add(ResultName);
                    SettingsManager.Save();
                }
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void NameComboBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                e.Handled = true;
                Save_Click(this, null);
            }
        }
    }
}
