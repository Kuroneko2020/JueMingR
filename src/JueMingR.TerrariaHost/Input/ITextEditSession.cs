using JueMingR.Features.Text;

namespace JueMingR.TerrariaHost.Input
{
    // Editing mechanics own input/IME, while each domain owns commands and
    // whether a completed edit may navigate after its asynchronous receipt.
    internal interface ITextEditSession
    {
        TextEditBuffer Editor { get; }
        bool RequestFinish();
        void CancelEdit();
        void PreserveUncommittedInput(TextEditBuffer editor);
    }
}
