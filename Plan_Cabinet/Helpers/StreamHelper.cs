namespace Plan_Cabinet.Helpers
{
    public static class StreamHelper
    {
        public static async Task<MemoryStream> ToSeekableMemoryStreamAsync(Stream input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            if (input.CanSeek)
            {
                input.Position = 0;
                // Make a copy to ensure the stream is not tied to the original source
                var buffer = new MemoryStream();
                await input.CopyToAsync(buffer);
                buffer.Position = 0;
                return buffer;
            }
            else
            {
                // If not seekable, fully buffer
                using var temp = new MemoryStream();
                await input.CopyToAsync(temp);
                var bytes = temp.ToArray();
                return new MemoryStream(bytes);
            }
        }
    }
}
