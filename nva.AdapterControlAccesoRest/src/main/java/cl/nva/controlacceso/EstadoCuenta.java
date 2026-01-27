package cl.nva.controlacceso;


public class EstadoCuenta {
    public String clienteId;  // ej: rut
    public double saldo;      // saldo > 0 => moroso; saldo <= 0 => al día

    @Override
    public String toString() {
        return "EstadoCuenta{clienteId='" + clienteId + "', saldo=" + saldo + "}";
    }
}
