﻿using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using DevExpress.XtraBars;
using DevExpress.XtraBars.Ribbon;
using DevExpress.XtraEditors;
using SPPaginationDemo.Filtration;
using SPPaginationDemo.Filtration.Custom;
using DevExpress.XtraGrid.Views.Grid;
using SP6LogicDemo;
using SPPaginationDemo.Extensions;
using SPPaginationDemo.Services;

namespace SPPaginatedGridControl;

public partial class MainForm : RibbonForm
{
    private readonly Sp7GridControl<CustomFiltrationParams, FiltrationHeader> _gridControl;

    public MainForm(int pageSize)
    {
        InitializeComponent();

        // Create an instance of the custom grid control
        _gridControl = new Sp7GridControl<CustomFiltrationParams, FiltrationHeader>
        {
            Dock = DockStyle.Fill,
            BaseUrl = "https://spagds-devwebapp.azurewebsites.net/",
            // BaseUrl = "https://localhost:7269/",
            ActionName = "DemoSelect",
            FiltrationParams = new CustomFiltrationParams
            {
                CustomFilter = "test",
                CurrentPage = 1,
                PageSize = pageSize
            }
        };

        _gridControl.TypeLoadingStopwatch.ObserveElapsed += (_, e) => bsiTypeLoadingElapsed.Caption = $@"Type Loading: {e} ms";
        _gridControl.DataFetchingStopwatch.ObserveElapsed += (_, e) => bsiDataFetchingElapsed.Caption = $@"Data Fetching: {e} ms";

        // Add the grid control to the MainForm
        pContent.Controls.Add(_gridControl);

        Load += OnLoad;
    }

    private void OnLoad(object? sender, EventArgs e)
    {
        var gridView = (GridView)_gridControl.MainView;
        gridView.OptionsView.ShowGroupPanel = false;
    }

    private void OnItemClick_bbiTestEndpoint(object sender, ItemClickEventArgs e)
    {
        try
        {
            var logic = new Logic();

            // Todo: DS: Add properties to Blueprint objetct and allow to pass the object to the server

            var processName = logic.GetProgramName();

            // Todo: DS: Add SignalR support to the server and client to avoid timeout errors

            XtraMessageBox.Show($"Hello from {processName}!");
        }
        catch (Exception exception)
        {
            XtraMessageBox.Show(exception.Message);
        }
    }

    private void OnItemClick_bbiEncryptPassword(object sender, ItemClickEventArgs e)
    {
        var passwordPlainText = XtraInputBox.Show("Enter password", "Encrypt password", null, MessageBoxButtons.OK);

        if (string.IsNullOrEmpty(passwordPlainText))
            return;

        var passwordBytes = Encoding.UTF8.GetBytes(passwordPlainText);

        //New RSA Parameters with public key
        var rsa = RSA.Create().ImportKeyAndCache(Path.Combine("ServerEncryptionKeys", "public_key.pem"));

        var encryptedPasswordBytes = passwordBytes.HybridEncrypt(rsa);
        var base64EncryptedPassword = Convert.ToBase64String(encryptedPasswordBytes);

        // Copy encrypted password to clipboard
        Clipboard.SetText(base64EncryptedPassword);
    }

    private void OnItemClick_bbiDebug(object sender, ItemClickEventArgs e)
    {
        throw new NotImplementedException();
    }
}