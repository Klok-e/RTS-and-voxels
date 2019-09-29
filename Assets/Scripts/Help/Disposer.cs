using System;
using System.Collections.Generic;

namespace Help
{
    public class Disposer
    {
        private readonly List<IDisposable> _toDispose;

        public Disposer(int size)
        {
            _toDispose = new List<IDisposable>(size);
        }

        public void Add(IDisposable disposable)
        {
            _toDispose.Add(disposable);
        }

        public void DisposeAll()
        {
            foreach (var item in _toDispose) item.Dispose();
        }
    }
}