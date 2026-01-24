using System;

namespace nva.Canonico
{
    public class EstadoCuentaCanonico
    {
        public int SchemaVersion { get; set; } = 1;
        public string ClienteId { get; set; }
        public int Saldo {  get; set; } // Saldo que devuelve Contable (positivo=deuda, 0 o negativo=al día)
        public bool AlDia { get; set; } // true = al día (sin deuda), false = con deuda
        public string ActualizadoEn { get; set; }
        public string Origen { get; set; } = "CONTABLE";
    }
}
