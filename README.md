# Ventagram

Ventagram es un portal de clasificados hecho en ASP.NET Core 8 con Razor Pages, MySQL y Entity Framework Core.

El producto quedó orientado a tres grandes rubros:

- `Inmuebles`
- `Rodados`
- `Generales`

También incorpora una lógica comunitaria para moderación:

- mensaje principal: `Basta de anuncios sin precios`
- denuncia de avisos por `No corresponde`, `Fraude` o `Sin precio`
- `Papelera` comunitaria con publicaciones denunciadas

## Estado actual

La app está montada en:

- `ASP.NET Core 8`
- `Razor Pages` como shell visual
- `Controllers MVC` para servir HTML parcial desde API
- `Pomelo.EntityFrameworkCore.MySql`
- `MySQL` como base principal

## Arquitectura actual

### Shell + HTML desde API

Las páginas Razor principales ya no renderizan el contenido completo del negocio en servidor. Ahora funcionan como host mínimo y cargan HTML desde endpoints `api/content/...`.

Páginas shell:

- [Pages/Index.cshtml](E:\Proyectos\ventagram\Pages\Index.cshtml)
- [Pages/Trash.cshtml](E:\Proyectos\ventagram\Pages\Trash.cshtml)
- [Pages/Publications/Details.cshtml](E:\Proyectos\ventagram\Pages\Publications\Details.cshtml)
- [Pages/Publications/Create.cshtml](E:\Proyectos\ventagram\Pages\Publications\Create.cshtml)

Controllers:

- [Controllers/ContentController.cs](E:\Proyectos\ventagram\Controllers\ContentController.cs)

Vistas parciales HTML servidas por controller:

- [Views/Content/Home.cshtml](E:\Proyectos\ventagram\Views\Content\Home.cshtml)
- [Views/Content/Trash.cshtml](E:\Proyectos\ventagram\Views\Content\Trash.cshtml)
- [Views/Content/Details.cshtml](E:\Proyectos\ventagram\Views\Content\Details.cshtml)
- [Views/Content/Create.cshtml](E:\Proyectos\ventagram\Views\Content\Create.cshtml)

Cliente:

- [wwwroot/js/site.js](E:\Proyectos\ventagram\wwwroot\js\site.js)

### Endpoints API actuales

- `GET /api/content/home`
- `GET /api/content/trash`
- `GET /api/content/details/{id}`
- `GET /api/content/create`
- `POST /api/content/report`
- `POST /api/content/create`

## Funcionalidad implementada

### Búsqueda y visualización

- búsqueda por grupo
- búsqueda por texto
- vistas:
  - `Mapa`
  - `Clasificado`
  - `Galería`

### Publicaciones

- alta por cuenta local
- alta con cuenta Google si se configuran credenciales
- alta anónima
- contraseña de baja para publicaciones anónimas
- detalle de publicación

### Moderación comunitaria

- botón `Denunciar` en listados
- motivos de denuncia:
  - `No corresponde`
  - `Fraude`
  - `Sin precio`
- página `Papelera` con publicaciones reportadas

## Modelo de datos

Se usa un modelo mixto:

- tabla principal de publicaciones
- detalle por rubro
- atributos extra dinámicos

Entidades relevantes:

- [Models/Publication.cs](E:\Proyectos\ventagram\Models\Publication.cs)
- [Models/PropertyDetail.cs](E:\Proyectos\ventagram\Models\PropertyDetail.cs)
- [Models/VehicleDetail.cs](E:\Proyectos\ventagram\Models\VehicleDetail.cs)
- [Models/GeneralDetail.cs](E:\Proyectos\ventagram\Models\GeneralDetail.cs)
- [Models/PublicationExtraAttribute.cs](E:\Proyectos\ventagram\Models\PublicationExtraAttribute.cs)
- [Models/PublicationReport.cs](E:\Proyectos\ventagram\Models\PublicationReport.cs)
- [Models/ApplicationUser.cs](E:\Proyectos\ventagram\Models\ApplicationUser.cs)

Persistencia:

- [Data/VentagramDbContext.cs](E:\Proyectos\ventagram\Data\VentagramDbContext.cs)
- [Data/SeedData.cs](E:\Proyectos\ventagram\Data\SeedData.cs)

## Base de datos

Proveedor configurado:

- `Pomelo.EntityFrameworkCore.MySql`

Cadena de desarrollo actual:

- [appsettings.Development.json](E:\Proyectos\ventagram\appsettings.Development.json)

Entorno de prueba definido:

- host: `127.0.0.1`
- puerto: `3307`
- usuario: `root`
- base: `ventagram`

## Autenticación

Soporta:

- usuario local con email, teléfono y contraseña
- Google, si se completa:
  - `Authentication:Google:ClientId`
  - `Authentication:Google:ClientSecret`

Configuración:

- [Program.cs](E:\Proyectos\ventagram\Program.cs)
- [Services/AuthService.cs](E:\Proyectos\ventagram\Services\AuthService.cs)

## Mapa

La vista de mapa usa MapTiler si se configura:

- `MapTiler:ApiKey`

Si no hay clave, se muestra placeholder.

## Estructura relevante

- `Controllers/`: endpoints MVC/API
- `Data/`: contexto EF y semilla
- `Models/`: entidades
- `Pages/`: shells Razor y flujos auxiliares
- `Services/`: lógica de negocio
- `ViewModels/`: modelos de render para controllers
- `Views/Content/`: HTML parcial servido por API
- `wwwroot/`: CSS y JS

## Ejecución

### Build

```powershell
dotnet build E:\Proyectos\ventagram\Ventagram.csproj
```

### Run

```powershell
dotnet run --project E:\Proyectos\ventagram\Ventagram.csproj --urls http://127.0.0.1:5099
```

## Estado Git

`E:\Proyectos\ventagram` hoy no es un repositorio Git. Por eso:

- no hay branch local para actualizar
- no se puede hacer `git status`, `commit` ni `switch` ahí

Si querés versionarlo, el siguiente paso sería inicializar Git o mover este proyecto dentro de un repo existente.
