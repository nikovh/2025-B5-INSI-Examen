namespace nva.Canonico
{
    public class PagoMeta
    {
        public string IngestadoEn { get; set; }          // ISO datetime
        public string CorrelationId { get; set; }        // para trazabilidad
        public string OrigenMensajeId { get; set; }      // si el origen trae un ID
    }
}