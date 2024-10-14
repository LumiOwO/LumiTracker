using Microsoft.Extensions.Logging;
using LumiTracker.Config;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;


namespace LumiTracker.Watcher
{
    public class GameEventMessage
    {
        [JsonProperty("type")]
        [JsonConverter(typeof(StringEnumConverter))]  // Optional: if you want TaskType as string
        public ETaskType TaskType { get; set; } = ETaskType.NONE;

        [JsonExtensionData] // Captures additional properties into this dictionary
        public Dictionary<string, JToken> Data { get; set; } = [];
    }

    public delegate void OnGenshinWindowFoundCallback();

    public delegate void OnWindowWatcherStartCallback(IntPtr hwnd);

    public delegate void OnWindowWatcherExitCallback();

    public delegate void OnGameStartedCallback();

    public delegate void OnMyActionCardPlayedCallback(int card_id);

    public delegate void OnOpActionCardPlayedCallback(int card_id);

    public delegate void OnGameOverCallback();

    public delegate void OnRoundDetectedCallback(int round);

    public delegate void OnMyCardsDrawnCallback(int[] card_ids);

    public delegate void OnMyCardsCreateDeckCallback(int[] card_ids);

    public delegate void OnOpCardsCreateDeckCallback(int[] card_ids);

    public delegate void OnUnsupportedRatioCallback();

    public delegate void OnCaptureTestDoneCallback(string filename, int width, int height);

    public delegate void OnLogFPSCallback(float fps);

    public delegate void ExceptionHandlerCallback(Exception ex);

    public delegate void OnGameEventMessageCallback(GameEventMessage message);

    public class GameEventHook
    {
        public event OnGenshinWindowFoundCallback? GenshinWindowFound;
        public event OnWindowWatcherStartCallback? WindowWatcherStart;
        public event OnWindowWatcherExitCallback?  WindowWatcherExit;
        public event OnGameStartedCallback?        GameStarted;
        public event OnMyActionCardPlayedCallback? MyActionCardPlayed;
        public event OnOpActionCardPlayedCallback? OpActionCardPlayed;
        public event OnGameOverCallback?           GameOver;
        public event OnRoundDetectedCallback?      RoundDetected;
        public event OnMyCardsDrawnCallback?       MyCardsDrawn;
        public event OnMyCardsCreateDeckCallback?  MyCardsCreateDeck;
        public event OnOpCardsCreateDeckCallback?  OpCardsCreateDeck;
        public event OnUnsupportedRatioCallback?   UnsupportedRatio;
        public event OnCaptureTestDoneCallback?    CaptureTestDone;
        public event OnLogFPSCallback?             LogFPS;
        public event ExceptionHandlerCallback?     ExceptionHandler;
        public event OnGameEventMessageCallback?   GameEventMessage;

        protected void InvokeGenshinWindowFound()
        {
            GenshinWindowFound?.Invoke();
        }

        protected void InvokeWindowWatcherStart(IntPtr hwnd)
        {
            WindowWatcherStart?.Invoke(hwnd);
        }

        protected void InvokeWindowWatcherExit()
        {
            WindowWatcherExit?.Invoke();
        }

        protected void InvokeGameStarted()
        {
            GameStarted?.Invoke();
        }

        protected void InvokeMyActionCardPlayed(int card_id)
        {
            MyActionCardPlayed?.Invoke(card_id);
        }

        protected void InvokeOpActionCardPlayed(int card_id)
        {
            OpActionCardPlayed?.Invoke(card_id);
        }

        protected void InvokeGameOver()
        {
            GameOver?.Invoke();
        }

        protected void InvokeRoundDetected(int round)
        {
            RoundDetected?.Invoke(round);
        }

        protected void InvokeMyCardsDrawn(int[] card_ids)
        {
            MyCardsDrawn?.Invoke(card_ids);
        }

        protected void InvokeMyCardsCreateDeck(int[] card_ids)
        {
            MyCardsCreateDeck?.Invoke(card_ids);
        }

        protected void InvokeOpCardsCreateDeck(int[] card_ids)
        {
            OpCardsCreateDeck?.Invoke(card_ids);
        }

        protected void InvokeUnsupportedRatio()
        {
            UnsupportedRatio?.Invoke();
        }

        protected void InvokeCaptureTestDone(string filename, int width, int height)
        {
            CaptureTestDone?.Invoke(filename, width, height);
        }

        protected void InvokeLogFPS(float fps)
        {
            LogFPS?.Invoke(fps);
        }

        protected void InvokeException(Exception e)
        {
            ExceptionHandler?.Invoke(e);
        }

        protected void InvokeGameEventMessage(GameEventMessage message)
        {
            GameEventMessage?.Invoke(message);
        }

        public void HookTo(GameEventHook other)
        {
            other.GenshinWindowFound += InvokeGenshinWindowFound;
            other.WindowWatcherStart += InvokeWindowWatcherStart;
            other.WindowWatcherExit  += InvokeWindowWatcherExit;
            other.GameStarted        += InvokeGameStarted;
            other.MyActionCardPlayed += InvokeMyActionCardPlayed;
            other.OpActionCardPlayed += InvokeOpActionCardPlayed;
            other.GameOver           += InvokeGameOver;
            other.RoundDetected      += InvokeRoundDetected;
            other.MyCardsDrawn       += InvokeMyCardsDrawn;
            other.MyCardsCreateDeck  += InvokeMyCardsCreateDeck;
            other.OpCardsCreateDeck  += InvokeOpCardsCreateDeck;
            other.UnsupportedRatio   += InvokeUnsupportedRatio;
            other.CaptureTestDone    += InvokeCaptureTestDone;
            other.LogFPS             += InvokeLogFPS;
            other.ExceptionHandler   += InvokeException;
            other.GameEventMessage   += InvokeGameEventMessage;
        }

        public void ParseGameEventMessage(GameEventMessage message)
        {
            ETaskType type = message.TaskType;
            if (type >= ETaskType.GAME_EVENT_FIRST && type <= ETaskType.GAME_EVENT_LAST)
            {
                InvokeGameEventMessage(message);
            }

            if (type == ETaskType.GAME_START)
            {
                Configuration.Logger.LogDebug("[GameEventHook] OnGameStarted");
                InvokeGameStarted();
            }
            else if (type == ETaskType.MY_PLAYED)
            {
                Configuration.Logger.LogDebug("[GameEventHook] OnMyActionCard");
                int card_id = message.Data["card_id"]!.ToObject<int>();
                InvokeMyActionCardPlayed(card_id);
            }
            else if (type == ETaskType.OP_PLAYED)
            {
                Configuration.Logger.LogDebug("[GameEventHook] OnOpActionCard");
                int card_id = message.Data["card_id"]!.ToObject<int>();
                InvokeOpActionCardPlayed(card_id);
            }
            else if (type == ETaskType.GAME_OVER)
            {
                Configuration.Logger.LogDebug("[GameEventHook] OnGameOver");
                InvokeGameOver();
            }
            else if (type == ETaskType.ROUND)
            {
                Configuration.Logger.LogDebug("[GameEventHook] OnRoundDetected");
                int round = message.Data["round"]!.ToObject<int>();
                InvokeRoundDetected(round);
            }
            else if (type == ETaskType.MY_DRAWN)
            {
                Configuration.Logger.LogDebug("[GameEventHook] OnMyCardsDrawn");
                int[] cards = message.Data["cards"]!.ToObject<int[]>()!;
                InvokeMyCardsDrawn(cards);
            }
            else if (type == ETaskType.MY_CREATE_DECK)
            {
                Configuration.Logger.LogDebug("[GameEventHook] OnMyCardsCreateDeck");
                int[] cards = message.Data["cards"]!.ToObject<int[]>()!;
                InvokeMyCardsCreateDeck(cards);
            }
            else if (type == ETaskType.OP_CREATE_DECK)
            {
                Configuration.Logger.LogDebug("[GameEventHook] OnOpCardsCreateDeck");
                int[] cards = message.Data["cards"]!.ToObject<int[]>()!;
                InvokeOpCardsCreateDeck(cards);
            }
            else if (type == ETaskType.UNSUPPORTED_RATIO)
            {
                Configuration.Logger.LogDebug("[GameEventHook] OnUnsupportedRatio");
                int client_width = message.Data["client_width"]!.ToObject<int>();
                int client_height = message.Data["client_height"]!.ToObject<int>();
                float ratio = 1.0f * client_width / client_height;
                Configuration.Logger.LogWarning(
                    $"[ProcessWatcher] Current resolution is {client_width} x {client_height} with ratio = {ratio}, which is not supported now.");
                InvokeUnsupportedRatio();
            }
            else if (type == ETaskType.CAPTURE_TEST)
            {
                Configuration.Logger.LogDebug("[GameEventHook] OnCaptureTestDone");
                string filename = message.Data["filename"]!.ToObject<string>()!;
                int width = message.Data["width"]!.ToObject<int>();
                int height = message.Data["height"]!.ToObject<int>();
                InvokeCaptureTestDone(filename, width, height);
            }
            else if (type == ETaskType.LOG_FPS)
            {
                //Configuration.Logger.LogDebug("[GameEventHook] OnLogFPS");
                float fps = message.Data["fps"]!.ToObject<float>()!;
                InvokeLogFPS(fps);
            }
            else
            {
                string task_type_name = type.ToString();
                Configuration.Logger.LogWarning($"[GameEventHook] Enum {task_type_name} defined but not handled: {task_type_name}\n{message.Data}");
            }
        }
    }
}
