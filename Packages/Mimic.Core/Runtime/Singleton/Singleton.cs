namespace Mimic.Core
{
    public enum SingletonInitializationStatus
    {
        None,
        Initializing,
        Initialized
    }

    public abstract class Singleton<T> : ISingleton where T : Singleton<T>, new()
    {
        private static T instance;
        private SingletonInitializationStatus initializationStatus = SingletonInitializationStatus.None;

        public static T Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (typeof(T))
                    {
                        if (instance == null)
                        {
                            instance = new T();
                            instance.InitializeSingleton();
                        }
                    }
                }

                return instance;
            }
        }

        public virtual bool IsInitialized => initializationStatus == SingletonInitializationStatus.Initialized;

        protected virtual void OnInitializing() { }
        protected virtual void OnInitialized() { }

        public virtual void InitializeSingleton()
        {
            if (initializationStatus != SingletonInitializationStatus.None)
            {
                return;
            }

            initializationStatus = SingletonInitializationStatus.Initializing;
            OnInitializing();
            initializationStatus = SingletonInitializationStatus.Initialized;
            OnInitialized();
        }

        public virtual void ClearSingleton() { }

        public static void CreateInstance()
        {
            DestroyInstance();
            instance = Instance;
        }

        public static void DestroyInstance()
        {
            if (instance == null)
            {
                return;
            }

            instance.ClearSingleton();
            instance = default;
        }
    }
}
