using System;

// Criatura que puede pasar de corrompida a purificada (y volver, si un desafío lo pide).
public interface IPurifiable
{
    bool IsPurified { get; }
    void SetPurified(bool purified);
    // Solo se dispara ante un cambio real de estado.
    event Action<IPurifiable> PurifiedChanged;
}
