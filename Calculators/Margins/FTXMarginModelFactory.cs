using System;

namespace Calculators.Margins
{
    public static class FTXMarginModelFactory
    {
        private static FTXMarginModel _ftxMarginModel;
        private static FTXMarginModelWithSpotMargin _ftxMarginModelWithSpotMargin;

        public static void Init(string location)
        {
            if (_ftxMarginModel is null)
            {
                _ftxMarginModel = new FTXMarginModel(location);
                _ftxMarginModelWithSpotMargin = new FTXMarginModelWithSpotMargin(location);
            }
        }

        public static IFTXMarginModel Resolve(bool enableSpotMargin)
        {
            if (_ftxMarginModel is null)
            {
                throw new Exception("Margin models not initialized, please call ctor with parameter location!");
            }
            return enableSpotMargin ? _ftxMarginModelWithSpotMargin : _ftxMarginModel;
        }
    }
}
 