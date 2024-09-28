using System.Collections.Generic;
using System.Windows;
using System.Windows.Media.Imaging;


namespace EasyPOS.CodeGen;
public partial class FileNameDialog : Window
{
    public FileNameDialog()
    {
        InitializeComponent();
        btnCreate.IsEnabled = false; 
    }

    // Property to get the TextBox value
    public string ClientPath => clientAbsolutePath.Text;
    public string ProvidedEntityName => entityName.Text;

    // Method that returns both TextBox values as a tuple
    public (string providedEntityName, string clientPath) GetInputValues()
    {
        return (ProvidedEntityName, ClientPath);
    }

    //Event handler for TextChanged event for both TextBoxes
    private void OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (entityName != null && clientAbsolutePath != null && btnCreate != null)
        {
            // Enable the button only if both TextBoxes have text
            btnCreate.IsEnabled = !string.IsNullOrWhiteSpace(entityName.Text) && !string.IsNullOrWhiteSpace(clientAbsolutePath.Text);
        }
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
