# RSMaps — principios para Web, Android e iOS

RSMaps debe evolucionar sin obligarnos a reescribir la lógica cuando existan clientes nativos para Android e iOS.

## Regla principal

La lógica de negocio, seguridad, multi-tenancy, permisos, búsquedas, coincidencias y acceso a datos debe vivir en el backend y ser reutilizable por cualquier cliente.

La web actual es un cliente de RSMaps, no debe convertirse en el lugar donde quede atrapada la lógica del producto.

## Arquitectura objetivo

```text
                 RSMaps Backend / API
                         │
        ┌────────────────┼────────────────┐
        │                │                │
      Web              Android           iOS
        │                │                │
        └────────────────┼────────────────┘
                         │
                  mismas reglas
                  mismos permisos
                  mismos filtros
                  mismos datos
```

## Decisiones que debemos mantener desde ahora

1. **Backend como fuente de verdad.** La autorización no debe depender de esconder botones en JavaScript. El servidor decide si una operación está permitida.
2. **API versionable.** Cuando expongamos endpoints para apps, usar contratos estables y versionados, por ejemplo `/api/v1/...`.
3. **DTOs/contratos, no tablas.** Los clientes móviles no deben conocer directamente la estructura interna de SQL Server.
4. **Autenticación desacoplada de la UI.** La web puede usar cookie durante la transición. Las apps móviles deberán usar un mecanismo apropiado para API, con tokens y renovación segura, sin duplicar las reglas de identidad/cuenta/rol.
5. **Cuenta activa explícita.** Un usuario podrá pertenecer a varias cuentas; Web, Android e iOS deberán trabajar con el mismo concepto de cuenta activa.
6. **Mapa como núcleo de búsqueda.** Ubicación, radio, polígonos, zona visible y filtros deben expresarse como parámetros de búsqueda reutilizables por todos los clientes.
7. **Precisión sobre volumen.** Los endpoints de búsqueda deben poder distinguir coincidencias exactas de alternativas cercanas, en lugar de devolver grandes listas poco relevantes.
8. **Fotos preparadas para móvil.** La futura carga debe soportar cámara, galería, compresión, reordenamiento, portada y almacenamiento externo; evitar depender para siempre de archivos locales del servidor web.
9. **Deep links.** Una propiedad compartida debe poder abrir la web o la app instalada en la ficha correcta.
10. **Notificaciones reutilizables.** Match prospecto-propiedad, invitaciones y seguimientos deberán generarse en backend para que después puedan enviarse por web, push, correo o mensajería.
11. **Borradores y conectividad variable.** La captura móvil futura debe contemplar guardar borradores y tolerar pérdida temporal de conexión sin perder fotografías o datos.
12. **Geolocalización nativa.** Android/iOS podrán usar GPS y sus SDK de mapas, pero deben enviar al backend los mismos conceptos de latitud, longitud, área y filtros.

## Evolución de autenticación

### Etapa actual

```text
Web
 ↓
POST usuario/contraseña
 ↓
Backend valida usuario + cuenta + rol
 ↓
Cookie web
```

### Etapa móvil futura

```text
Android / iOS
      ↓
API de autenticación
      ↓
Backend valida la misma identidad + cuenta + rol
      ↓
Token de acceso / renovación
      ↓
API RSMaps
```

La lógica que determina quién es el usuario, a qué cuenta pertenece y qué rol posee no debe duplicarse en las apps.

## Experiencias móviles que queremos posibilitar

- registrar una propiedad desde el lugar usando GPS;
- tomar fotos directamente con la cámara y ordenarlas antes de publicar;
- buscar moviendo/dibujando sobre el mapa;
- guardar búsquedas y recibir alertas cuando aparece una coincidencia real;
- abrir una propiedad desde un enlace de WhatsApp directamente en su ficha;
- preparar recorridos de varias propiedades para un cliente;
- trabajar con una cuenta individual y varias inmobiliarias desde una sola identidad;
- recibir alertas de prospectos compatibles con una propiedad recién capturada.

Estas capacidades deben surgir de una arquitectura común, no de tres aplicaciones con reglas diferentes.
