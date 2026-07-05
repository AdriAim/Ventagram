# Guía de texto para vistas

Usar esta guía al editar `.cshtml`, `.cs`, `.json` y textos visibles de la app.

## Reglas

- Mantener acentos, eñes y signos de apertura/cierre en español: `publicación`, `contraseña`, `¿`, `¡`.
- No convertir textos de interfaz a ASCII plano.
- No “normalizar” palabras visibles quitando tildes solo por comodidad del editor.
- Guardar los archivos en UTF-8.
- Si una vista ya usa acentos, conservar el mismo estilo en las modificaciones nuevas.

## Ejemplos correctos

- `Ingresá a tu cuenta`
- `Crear publicación`
- `¿Olvidaste tu contraseña?`
- `Sólo se mostrarán en la publicación los datos de contacto que marques`

## Ejemplos a evitar

- `Ingresa a tu cuenta`
- `Crear publicacion`
- `Olvidaste tu contrasena?`

## Nota práctica

Si al editar una vista aparecen caracteres rotos, revisar el encoding del archivo antes de seguir tocando texto visible. No reemplazar acentos por ASCII para “arreglar” el problema.
