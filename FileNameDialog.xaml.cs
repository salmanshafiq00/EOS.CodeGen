﻿using System.Collections.Generic;
using System.Windows;
using System.Windows.Media.Imaging;


namespace EOS.CodeGen;
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
    public string CqrsAbsPath => cqrsParentAbsPath.Text;

    // Method that returns both TextBox values as a tuple
    public (string providedEntityName, string clientPath, string cqrsAbsPath) GetInputValues()
    {
        return (ProvidedEntityName, ClientPath, CqrsAbsPath);
    }

    //Event handler for TextChanged event for both TextBoxes
    private void OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (entityName != null && clientAbsolutePath != null && cqrsParentAbsPath != null && btnCreate != null)
        {
            // Enable the button only if both TextBoxes have text
            btnCreate.IsEnabled = !string.IsNullOrWhiteSpace(entityName.Text) && (!string.IsNullOrWhiteSpace(clientAbsolutePath.Text) || !string.IsNullOrWhiteSpace(cqrsParentAbsPath.Text));
        }
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
