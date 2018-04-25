using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace Plugins.Helpers
{
    public static class UnityThread
    {
        private static Executer instance;

        public static void InitUnityThread()
        {
            if (instance != null)
            {
                throw new InvalidOperationException();
            }

            instance = new GameObject("MainThreadExecuter").AddComponent<Executer>();
            instance.Initialize();
        }

        public static void ExecuteInUpdate(Action action)
        {
            Debug.Assert(instance != null);
            if (action == null)
            {
                throw new ArgumentNullException();
            }
            instance.AddToQueue(action);
        }

        public static void StartCoroutine(IEnumerator action)
        {
            Debug.Assert(instance != null);
            if (action == null)
            {
                throw new ArgumentNullException();
            }
            instance.AddToQueue(() => instance.StartCoroutine(action));
        }

        private class Executer : MonoBehaviour
        {
            private volatile bool noActionQueueToExecuteUpdateFunc = true;

            private ConcurrentQueue<Action> actionQueueUpdateFunc;

            public void Initialize()
            {
                actionQueueUpdateFunc = new ConcurrentQueue<Action>();
            }

            public void AddToQueue(Action action)
            {
                actionQueueUpdateFunc.Enqueue(action);
                noActionQueueToExecuteUpdateFunc = false;
            }

            private void Update()
            {
                if (noActionQueueToExecuteUpdateFunc)
                {
                    return;
                }

                while (actionQueueUpdateFunc.Count != 0)
                {
                    Action item;
                    if (actionQueueUpdateFunc.TryDequeue(out item))
                    {
                        item.Invoke();
                    }
                }
                noActionQueueToExecuteUpdateFunc = true;
            }
        }
    }
}
