namespace Unity.Entities.Editor
{
    /// <summary>
    /// https://en.wikipedia.org/wiki/Moving_average#Exponential_moving_average
    /// </summary>
    struct ExponentialMovingAverage
    {
        public static void Add(ref float current, float next, ref float variance, int n = 10)
        {
            if (current == 0)
            {
                current = next;
                return;
            }
            
            var alpha = 2.0f / (n + 1);
            var delta = next - current;
            current += alpha * delta;
            variance = (1 - alpha) * (variance + alpha * delta * delta);
        }
    }
}