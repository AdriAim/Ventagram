# Denuncias y moderación automática

Fecha de cierre funcional: 30/07/2026

## Objetivo

Definir un flujo simple y automático para denuncias de publicaciones, con revisión administrativa posterior, sin intervención manual previa para aplicar la medida inicial.

## Reglas principales

### Quién puede denunciar

Un usuario solo puede denunciar si:

- inició sesión,
- tiene al menos 2 publicaciones realizadas,
- no fue bloqueado para denunciar.

### Cuándo una denuncia cuenta

- solo cuenta una denuncia por usuario por publicación,
- la publicación denunciada no puede ser del mismo usuario denunciante,
- las denuncias que cuentan para el umbral son las pendientes de revisión.

### Umbral de aviso

Cuando una publicación recibe 5 denuncias de 5 usuarios diferentes:

- la publicación sigue activa,
- el publicante recibe un correo avisando que revise los datos, imágenes y contenido.

### Umbral de papelera

Cuando una publicación recibe 10 denuncias de 10 usuarios diferentes:

- la publicación pasa automáticamente a papelera,
- la publicación deja de estar activa,
- el usuario queda bloqueado para publicar nuevos anuncios,
- el publicante recibe un correo informando la medida,
- el caso entra en la bandeja de revisión administrativa.

### Denunciantes abusivos

Cuando un usuario acumula 5 denuncias incorrectas:

- pierde la posibilidad de denunciar nuevas publicaciones.

Una denuncia incorrecta es una denuncia rechazada por el administrador al revisar un caso.

## Revisión administrativa

La bandeja administrativa muestra:

- publicación,
- publicante,
- cantidad de denuncias pendientes,
- cantidad de denunciantes distintos,
- motivos,
- último comentario asociado.

Acciones:

- Restaurar y habilitar:
  - saca la publicación de papelera,
  - vuelve a activar la publicación,
  - vuelve a habilitar al usuario para publicar,
  - marca las denuncias pendientes como rechazadas.

- Confirmar papelera:
  - mantiene la publicación en papelera,
  - mantiene al usuario bloqueado para publicar,
  - marca las denuncias pendientes como confirmadas.

## Estados usados

### Usuario

- `CanPublish = true|false`
- `CanReport = true|false`
- `IsAdmin = true|false`

### Publicación

- `Status`
- `ModerationStatus`
- `IsActive`
- `ReportWarningSentAtUtc`
- `ReportTrashSentAtUtc`
- `TrashedAtUtc`

Estados de moderación usados:

- `None`
- `Reported`
- `Warned`
- `PendingReview`
- `Confirmed`
- `Restored`

### Denuncia

- `ReporterUserId`
- `CountsTowardThreshold`
- `ReviewStatus`
- `ReviewedAtUtc`
- `ReviewedByUserId`

Estados de revisión:

- `Pending`
- `Confirmed`
- `Rejected`

## Correos automáticos

### Correo a las 5 denuncias

Asunto:

`Tu publicación recibió denuncias`

Mensaje:

- informa que la publicación recibió varias denuncias,
- pide revisar información, imágenes y datos,
- aclara que la publicación sigue visible.

### Correo a las 10 denuncias

Asunto:

`Tu publicación pasó a revisión`

Mensaje:

- informa que la publicación fue enviada a papelera,
- informa que no podrá publicar nuevos anuncios hasta nuevo aviso,
- aclara que un administrador revisará el caso.

## UX implementada

- el botón `Denunciar` queda visible,
- si el usuario no inició sesión, se muestra un modal indicando que debe iniciar sesión,
- si no tiene al menos 2 publicaciones, se muestra un modal explicando la restricción,
- si fue bloqueado para denunciar, se muestra un modal explicando el bloqueo,
- si puede denunciar, se abre el modal normal de denuncia.

## Texto visible para el publicante

Se agregó explicación en:

- pantalla de creación de publicación,
- pantalla legal del sitio,
- panel de mis publicaciones cuando la cuenta está bloqueada.

## Pendientes recomendados

- definir desde base de datos qué usuarios serán administradores,
- auditar si conviene excluir publicaciones inactivas del conteo de “2 publicaciones realizadas”,
- agregar historial administrativo más detallado si luego se necesita trazabilidad formal.
