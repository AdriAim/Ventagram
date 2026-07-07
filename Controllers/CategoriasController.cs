using Microsoft.AspNetCore.Mvc;
using Ventagram.Services;

namespace Ventagram.Controllers;

[ApiController]
[Route("api/categorias")]
public class CategoriasController(PublicationCategoryFieldService publicationCategoryFieldService) : ControllerBase
{
    [HttpGet("{id:int}/campos")]
    public async Task<IActionResult> GetCampos(int id)
    {
        var fields = await publicationCategoryFieldService.GetActiveByCategoryIdAsync(id);

        return Ok(fields.Select(x => new
        {
            id = x.Id,
            nombreInterno = x.InternalName,
            etiqueta = x.Label,
            tipoDato = x.DataType.ToString().ToLowerInvariant(),
            obligatorio = x.Required,
            orden = x.SortOrder,
            unidad = x.Unit,
            opciones = string.IsNullOrWhiteSpace(x.OptionsCsv)
                ? Array.Empty<string>()
                : x.OptionsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        }));
    }
}
