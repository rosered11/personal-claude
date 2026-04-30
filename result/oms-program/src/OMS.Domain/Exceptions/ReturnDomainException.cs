namespace OMS.Domain.Exceptions;

public class ReturnDomainException : Exception
{
    public ReturnDomainException(string message) : base(message) { }
    public ReturnDomainException(string message, Exception inner) : base(message, inner) { }
}
