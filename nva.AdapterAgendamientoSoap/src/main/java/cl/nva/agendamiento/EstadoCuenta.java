package cl.nva.agendamiento;

public class EstadoCuenta {
    public String clienteId; // ej: rut
    public double saldo;     // <= 0 => al día

    @Override
    public String toString() {
        return "EstadoCuenta{clienteId='" + clienteId + "', saldo=" + saldo + "}";
    }
}
