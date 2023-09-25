namespace OneInch.Net
{
    public class CallResult<T>
    {
        public T? Data;
        public CallError? Error;
        public bool Success => Error == null;

        public CallResult(T data)
        {
            Data = data;
        }

        public CallResult(CallError error)
        {
            Error = error;
        }
    }
}