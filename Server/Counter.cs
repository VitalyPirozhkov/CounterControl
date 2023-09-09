namespace Server
{
    using System.Threading.Tasks;

    public class Counter
    {
        private int value = 0;
        private bool isRunning = false;

        public int Value => value;
        public bool IsRunning => isRunning;

        public void Start()
        {
            isRunning = true;
            Task.Run(async () =>
            {
                while (isRunning)
                {
                    value++;
                    await Task.Delay(1000); // Изменение значения каждую секунду
                }
            });
        }

        public void Stop()
        {
            isRunning = false;
        }

        public void Reset()
        {
            value = 0;
        }
    }

}
