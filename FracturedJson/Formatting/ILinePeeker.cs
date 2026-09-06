namespace FracturedJson.Formatting;

internal interface ILinePeeker : IBuffer
{
    public string PeekCurrentLine();
}
