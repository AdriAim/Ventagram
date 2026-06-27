# Decisiones Técnicas

## 1. Base tecnológica

- ASP.NET Core 8
- Razor Pages para shells simples
- Controllers MVC para servir HTML parcial
- EF Core 8 con Pomelo
- MySQL como base principal

## 2. Arquitectura de render

Se decidió usar `HTML servido desde API` para las pantallas principales en lugar de dejar toda la UI renderizada directamente por Razor Pages.

Razones:

- permite desacoplar el shell del contenido
- facilita mover partes a endpoints reutilizables
- deja un camino más simple para una futura SPA parcial sin reescribir negocio

## 3. Modelo de publicaciones

Se mantuvo un modelo mixto:

- publicación base
- detalle por rubro
- atributos extra dinámicos

Esto evita rigidez extrema sin ir a un EAV puro desde el día uno.

## 4. Moderación comunitaria

Se incorporó una lógica explícita de comunidad:

- denuncias por motivo
- mensaje editorial contra anuncios sin precio
- papelera pública con avisos reportados

## 5. Alta de publicaciones

Se soportan tres modos:

- cuenta local
- cuenta Google
- publicación anónima

La publicación anónima genera:

- vencimiento a 30 días
- contraseña de baja

## 6. Diseño visual

Se trabajó con una estética inspirada en Airbnb, pero adaptada a un tono más editorial/comunitario:

- tipografía serif + sans contrastada
- pasteles cálidos
- tarjetas grandes
- botones redondeados
- lenguaje visual de comunidad y moderación

## 7. Alcance del refactor actual

Ya migrado a API + controllers:

- home
- papelera
- detalle
- create

Pendiente de migrar si se busca homogeneidad total:

- login
- register
- delete anonymous
