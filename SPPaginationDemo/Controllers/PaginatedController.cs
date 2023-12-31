using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using SPPaginationDemo.Filtration.Custom;
using SPPaginationDemo.Services;
using SPPaginationDemo.SqlQueries;

#pragma warning disable IDE0290
#pragma warning disable CS1998

namespace SPPaginationDemo.Controllers;

public class PaginatedController : Sp7ControllerBase
{
    public PaginatedController(IMapper mapper, Appsettings appsettings, ILogger logger) : base(mapper, appsettings, logger) { }

    public class DemoSelect : Endpoint<IDemoSelect, CustomFiltrationParams>
    {
        public override IQueryable<TGenerated> QueryFilter<TGenerated>(IQueryable<TGenerated> queryable, CustomFiltrationParams filtrationParams)
        {
            // Todo: DS: Replace EntityFrameworkCore with Dapper

            var orderedQuery = queryable
                .Where(d => filtrationParams.CustomFilter != null && d.Vorname != null && d.Vorname.ToUpper().Contains(filtrationParams.CustomFilter))
                .OrderBy(d => d.STID)
                .ThenBy(d => d.AGID)
                .ThenBy(d => d.Name)
                .ThenBy(d => d.Vorname);

            return orderedQuery;
        }

        public override async Task<IActionResult> InMemoryProcessingAsync<TGenerated>(List<TGenerated> paginatedResult, CustomFiltrationParams filtrationParams)
        {
            // Todo: DS: List to DataTable extension method

            paginatedResult.ForEach(d => d.Vorname = d.Vorname?.ToUpper());

            // Todo: DS: Make DataTable serializable

            return ControllerBase.Ok(paginatedResult);
        }
    }
}