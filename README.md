# Examen INSI

Todos los recursos ejecutables permiten configurar el **puerto de ejecución** pasando el puerto como argumento al momento de iniciar la aplicación.
Por ejemplo:

```
.\SistemaXYZ.exe <puerto>
```


## XML Pagos

Para el desarrollo de la solución se entregan **tres archivos XML**, los cuales contienen la información necesaria para simular y procesar los pagos realizados por los clientes en sucursales.


## Web Pagos (REST)

Aplicación web que expone una **API REST** para la consulta de pagos.

- **Puerto por defecto:** 5000
- **Endpoints disponibles:**
  - `GET /api/payments` — Obtiene el listado completo de pagos registrados.
  - `GET /api/payments/today` — Obtiene los pagos realizados durante el día actual.

Para ejecutar el sistema:

```
.\WebPagos.exe <puerto>
```


## Sistema Contable (SOAP)

Sistema contable expuesto mediante **servicios SOAP**.

- **Puerto por defecto:** 5001
- **WSDL:** `http://localhost:5001/?wsdl`
- **Plataforma:** Solo Windows
- **Requisito:** .NET Framework 4.8.1

Para ejecutar el sistema:

```
.\SistemaContable.exe <puerto>
```


### Métodos disponibles

- `RegistrarPago(clienteId, monto)`

Este método registra un pago y retorna el **estado de cuenta** del cliente, incluyendo:
- `clienteId`
- `saldo`

**Interpretación del saldo:**
- Saldo **mayor a 0**: el cliente mantiene deuda.
- Saldo **igual o menor a 0**: el cliente se encuentra al día o ha pagado por adelantado.



## Sistema de Agendamiento (SOAP)

Sistema SOAP encargado de la **gestión de clases del gimnasio**.

- **Puerto por defecto:** 5002
- **WSDL:** `http://localhost:5002/AgendaService?wsdl`
- **Requisito:** .NET 8

Se recomienda su ejecución en Linux mediante el siguiente comando:

```
dotnet SistemaAgendamientoClases.dll <puerto>
```


### Métodos disponibles

- `HabilitarUsuario(clienteId)` — Habilita al cliente para agendar clases.
- `DeshabilitarUsuario(clienteId)` — Deshabilita al cliente para agendar clases.


## Sistema de Control de Acceso (REST)

Sistema encargado de la **gestión de usuarios y control de acceso**, expuesto mediante una **API REST**. Requiere .NET 10 instalado.

Para ejecutar en Linux:

```
dotnet SistemaControlAcceso.dll
```

### Endpoints disponibles

- `GET /api/users` — Recupera la lista de usuarios.
- `POST /api/users` — Crea un nuevo usuario.
- `GET /api/users/{rut}` — Recupera la información de un usuario específico.
- `PATCH /api/users/{rut}` — Actualiza la información de un usuario.
- `DELETE /api/users/{rut}` — Elimina un usuario.

### Formato JSON esperado

```
{
"rut": "string",
"nombre": "string",
"email": "string",
"telefono": "string",
"habilitado": boolean
}
```
