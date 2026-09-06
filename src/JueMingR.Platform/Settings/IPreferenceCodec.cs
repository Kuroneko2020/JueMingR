namespace JueMingR.Platform.Settings
{
    // Codecs own document meaning; the storage port only sees bounded bytes.
    public interface IPreferenceCodec<T>
    {
        T Decode(byte[] contents);
        byte[] Encode(T value);
    }
}
