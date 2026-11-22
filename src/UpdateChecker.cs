using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace JWFixMonitor
{
    /// <summary>
    /// Chiede a GitHub qual è l'ultima versione pubblicata. Lavora su un thread a parte;
    /// se c'è una versione più recente lo segnala con <see cref="Found"/>.
    /// Senza rete o in caso di errore non dice nulla (lo scrive solo nel registro).
    /// </summary>
    internal sealed class UpdateChecker : IDisposable
    {
        private static readonly TimeSpan FirstCheck = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan Every = TimeSpan.FromHours(12);

        private readonly Version _current;
        private readonly Timer _timer;
        private int _checking;

        /// <summary>Trovata in automatico una versione nuova (una volta per versione). Chiamato dal thread di lavoro.</summary>
        public event Action<Release> Found;

        /// <summary>Ultima release più recente di quella in uso, se trovata.</summary>
        public Release Available { get; private set; }

        public UpdateChecker(Version current)
        {
            _current = current;
            _timer = new Timer(_ => Check(manual: false), null, Timeout.Infinite, Timeout.Infinite);
        }

        private bool _running;

        /// <summary>Avvia i controlli periodici (nulla se già avviati).</summary>
        public void Start()
        {
            if (!_running)
            {
                _running = true;
                _timer.Change(FirstCheck, Every);
            }
        }

        public void Stop()
        {
            _running = false;
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>Controlla subito. <paramref name="done"/> riceve la release più recente (o null) e l'eventuale errore.</summary>
        public void CheckNow(Action<Release, string> done) =>
            ThreadPool.QueueUserWorkItem(_ => Check(manual: true, done: done));

        private void Check(bool manual, Action<Release, string> done = null)
        {
            if (Interlocked.CompareExchange(ref _checking, 1, 0) != 0)
            {
                return;
            }

            Release newer = null;
            string error = null;
            try
            {
                var latest = Updates.Parse(Download(Updates.LatestApi));
                if (latest == null)
                {
                    error = "risposta non valida";
                }
                else if (Updates.IsNewer(latest.Tag, _current))
                {
                    newer = latest;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
            finally
            {
                Interlocked.Exchange(ref _checking, 0);
            }

            if (error != null)
            {
                Log.Write("Controllo aggiornamenti non riuscito: " + error);
            }
            else if (newer != null)
            {
                var isNew = Available == null || Available.Tag != newer.Tag;
                Available = newer;
                if (isNew)
                {
                    Log.Write("Nuova versione disponibile: " + newer.Tag);
                }

                // controllo a mano: risponde done; in automatico avviso una volta per versione
                if (isNew && !manual)
                {
                    Found?.Invoke(newer);
                }
            }

            done?.Invoke(newer, error);
        }

        private string Download(string url)
        {
            // .NET Framework 4.8 non sempre attiva TLS 1.2 da solo, e GitHub lo richiede
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            var request = (HttpWebRequest)WebRequest.Create(url);
            request.UserAgent = "JWFixMonitor/" + _current;
            request.Accept = "application/vnd.github+json";
            request.Timeout = 15000;
            request.ReadWriteTimeout = 15000;

            using (var response = request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        public void Dispose() => _timer.Dispose();
    }
}
