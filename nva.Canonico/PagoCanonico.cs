using System;

namespace nva.Canonico
{
    public class PagoCanonico
    {
        public int SchemaVersion { get; set; } = 1;

        // Id determinístico (para idempotencia). Se genera en los Translators.
        public string PagoId { get; set; }

        // corresponde al RUT (con DV) de la persona.
        public string ClienteId { get; set; }

        public int Monto { get; set; }
        public string Moneda { get; set; } = "CLP";

        // "WEB" o "SUCURSAL"
        public OrigenPago Origen { get; set; }

        // ISO date (yyyy-MM-dd)
        public string FechaPago { get; set; }

        // "TC", "TD", "EF"
        public MedioPago MedioPago { get; set; }

        // Opcionales según origen
        public string CodigoAutorizacion { get; set; }  // null si no aplica
        public string TarjetaUltimos4 { get; set; }     // null si no aplica
        public string SucursalId { get; set; }          // null si Origen=WEB

        public PagoMeta Meta { get; set; } = new PagoMeta();
    }
}