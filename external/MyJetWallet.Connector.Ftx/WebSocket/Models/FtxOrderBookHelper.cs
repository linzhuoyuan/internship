namespace FtxApi.WebSocket.Models
{
    public static class FtxOrderBookHelper
    {
        public static decimal? GetFtxOrderBookPrice(this decimal?[] array)
        {
            if (array.Length < 1)
            {
                return null;
            }

            return array[0];
        }

        public static decimal? GetFtxOrderBookVolume(this decimal?[] array)
        {
            if (array.Length < 2)
            {
                return null;
            }

            return array[1];
        }
    }
}