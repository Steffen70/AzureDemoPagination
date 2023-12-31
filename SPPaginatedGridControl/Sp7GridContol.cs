﻿using System.Collections;
using System.Net.Http.Headers;
using System.Text;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraGrid.Views.Grid.ViewInfo;
using SPPaginationDemo.Filtration;
using JsonSerializer = System.Text.Json.JsonSerializer;

// ReSharper disable RedundantCatchClause
#pragma warning disable CS0168

namespace SPPaginatedGridControl;

public sealed class Sp7GridControl<TFiltrationParams, TFiltrationHeader> : GridControl where TFiltrationParams : FiltrationParams where TFiltrationHeader : FiltrationHeader
{
    // Todo: DS: Override find panel to use the custom filtration header
    // Todo: DS: Disable data fetching when sorting or filtering

    public readonly CustomStopwatch TypeLoadingStopwatch = new();
    public readonly CustomStopwatch DataFetchingStopwatch = new();

    private dynamic _defaultDataSource = null!;

    private HttpClient _client = null!;
    private Type? _modelType;

    private GridView _view = null!;

    private TFiltrationHeader? _filtrationHeader;

    public event EventHandler<TFiltrationHeader>? FiltrationHeaderChanged;

    public string BaseUrl { get; set; } = null!;
    public string ActionName { get; set; } = null!;
    public TFiltrationParams FiltrationParams { get; set; } = null!;

    public TFiltrationHeader? FiltrationHeader
    {
        get => _filtrationHeader;
        private set
        {
            if (value == null) return;

            FiltrationHeaderChanged?.Invoke(this, value);

            _filtrationHeader = value;
        }
    }

    public Sp7GridControl()
    {
        Load += OnLoad;

        DoubleBuffered = true;
    }

    private void OnLoad(object? sender, EventArgs e)
    {
        _client = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl)
        };

        // Add default headers as necessary
        _client.DefaultRequestHeaders.Accept.Clear();
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _client.DefaultRequestHeaders.Add("User-Agent", "Sp7GridControl");

        _view = (GridView)MainView;
        _view.TopRowChanged += ViewTopRowChanged;
        Resize += ViewTopRowChanged;
            
        _ = InitializeAsync();
    }

    private volatile bool _dataFetched;
    private volatile bool _isFetchingData;

    private void ViewTopRowChanged(object? sender, EventArgs e)
    {
        if (_isFetchingData || _dataFetched) return;

        _isFetchingData = true;
        _ = ViewTopRowChangedAsync(sender, e).ContinueWith(t => _isFetchingData = false);
    }

    private async Task ViewTopRowChangedAsync(object? sender, EventArgs e)
    {
        // Calculate the approximate number of visible rows
        var visibleRowCount = _view.GridControl.Height / ((GridViewInfo)_view.GetViewInfo()).ColumnRowHeight;

        // Calculate the number of rows hidden below the viewport
        var hiddenRowCount = _view.RowCount - _view.TopRowIndex - visibleRowCount;

        // If there are fewer than FiltrationParams.PageSize rows hidden, fetch more data

        if (hiddenRowCount < FiltrationParams.PageSize)
            _dataFetched = await FetchData();

        ViewTopRowChanged(this, EventArgs.Empty);
    }

    private async Task InitializeAsync()
    {
        try
        {
            _modelType = await _client.LoadTypeFromServerOrCacheAsync(ActionName, TypeLoadingStopwatch);

            _defaultDataSource = Activator.CreateInstance(typeof(List<>).MakeGenericType(_modelType))!;
            _view.GridControl.DataSource = _defaultDataSource;

            _isFetchingData = true;
            DataFetchingStopwatch.Start();

            // Fetch the first page of data
            await FetchData();

            DataFetchingStopwatch.Stop();
            _isFetchingData = false;

            // Configure columns
            // Create columns for all fields in the data source
            _view.PopulateColumns();
            // Resize columns to fit all cells
            _view.BestFitColumns();

            // ReSharper disable once MethodHasAsyncOverload
            ViewTopRowChanged(this, EventArgs.Empty);
        }
        catch (Exception _)
        {
            // Todo: DS: Handle exception when the REST API is not available or _modelType is not set
            throw;
        }
    }

    private async Task<bool> FetchData()
    {
        if (_modelType == null) throw new InvalidOperationException("The model type is not set.");

        try
        {
            // Prepare the request
            var request = new HttpRequestMessage(HttpMethod.Get, $"/Paginated/{ActionName}");
            var jsonParams = JsonSerializer.Serialize(FiltrationParams, HttpExtensions.Options);
            request.Content = new StringContent(jsonParams, Encoding.UTF8, "application/json");

            // Send the request
            var response = await _client.SendAsync(request);

            // Ensure we have a successful status code
            if (!response.IsSuccessStatusCode)
                return false;

            FiltrationParams.CurrentPage++;

            // Get the response header
            if (response.Headers.TryGetValues("Filtration", out var headerValues))
                FiltrationHeader = JsonSerializer.Deserialize<TFiltrationHeader>(headerValues.First(), HttpExtensions.Options);

            // Deserialize the body content
            var responseBody = await response.Content.ReadAsStringAsync();

            var genericListType = typeof(List<>);
            var specificListType = genericListType.MakeGenericType(_modelType);

            var data = (dynamic)JsonSerializer.Deserialize(responseBody, specificListType, HttpExtensions.Options)!;

            // Add the data to the Grid's DataSource
            _defaultDataSource.AddRange(data);

            return (FiltrationHeader?.CurrentPage ?? 1) == FiltrationHeader?.TotalPages;
        }
        catch (Exception _)
        {
            // Todo: DS: Handle exception when fetching data from the REST API fails
            return false;
        }
    }
}