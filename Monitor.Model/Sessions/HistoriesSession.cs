using System.ComponentModel;

namespace Monitor.Model.Sessions
{
    public class HistoriesSession : JsonSession
    {
        private class ResultItem
        {
            public int Id { get; set; }
            public string Data { get; set; }
        }

        private readonly string _filename;

        protected override void DoWork(object sender, DoWorkEventArgs e)
        {
            var database = new LiteDB.LiteDatabase(_filename);
            var collection = database.GetCollection<ResultItem>("result");
            foreach (var item in collection.FindAll())
            {
                ProcessPacket(item.Data);
            }
        }

        public HistoriesSession(
            ISessionHandler sessionHandler,
            IResultConverter resultConverter,
            HistoriesSessionParameters parameters):base(sessionHandler, resultConverter)
        {
            closeAfterCompleted = parameters.CloseAfterCompleted;
            _filename = parameters.FileName;
        }

        public override string Name => "Histories";
    }
}