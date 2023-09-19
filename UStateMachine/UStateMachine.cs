using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

namespace UStateMachine
{
    /// <summary>
    /// ステートマシン
    /// </summary>
    /// <typeparam name="TContext"></typeparam>
    public sealed class UStateMachine<TContext> where TContext : class
    {
        /// <summary>
        /// ステート基底クラス
        /// </summary>
        public abstract class UState
        {
#pragma warning disable CS8618
            private readonly HashSet<Type> destination = new();
            /// <summary>
            /// 所属中のステートマシン
            /// </summary>
            protected internal UStateMachine<TContext> StateMachine { get; internal set; }

            /// <summary>
            /// コンテキスト
            /// </summary>
            protected internal TContext Context { get; internal set; }

            /// <summary>
            /// ステート突入
            /// </summary>
            protected internal virtual void Entry() { }

            /// <summary>
            /// ステート更新
            /// </summary>
            protected internal virtual void Update() { }

            /// <summary>
            /// ステート退出
            /// </summary>
            protected internal virtual void Exit() { }

            /// <summary>
            /// 推移先の追加
            /// </summary>
            /// <typeparam name="T"></typeparam>
            public void RegisterTransition<T>() where T : UState => destination.Add(typeof(T));

            /// <summary>
            /// 推移先の削除
            /// </summary>
            /// <typeparam name="T"></typeparam>
            public void UnRegisterTransition<T>() where T : UState => destination.Remove(typeof(T));

            /// <summary>
            /// 推移先の検索
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <returns></returns>
            public bool FindState<T>() where T : UState => destination.Contains(typeof(T));
#pragma warning restore CS8618
        }

        /// <summary>
        /// 例外処理の設定
        /// </summary>
        public enum UnHandleExceptionMode
        {
            /// <summary>
            /// 例外をそのまま投げる
            /// </summary>
            ThrowException,

            /// <summary>
            /// 例外をハンドラに転送
            /// </summary>
            CatchException
        }

        /// <summary>
        /// ステートマシンの内部状態
        /// </summary>
        public enum MachineState
        {
            /// <summary>
            /// 待機
            /// </summary>
            Idle,

            /// <summary>
            /// 突入
            /// </summary>
            Entry,

            /// <summary>
            /// 更新
            /// </summary>
            Update,

            /// <summary>
            /// 退出
            /// </summary>
            Exit
        }

        #region StateMachine MemberVariables
        /// <summary>
        /// 登録されたステート一覧
        /// </summary>
        private readonly Dictionary<Type, UState> stateList;

        private int lastThreadID;
        #endregion

        #region StateMachine Propertys
        /// <summary>
        /// コンテキスト
        /// </summary>
        public TContext Context { get; private set; }

        /// <summary>
        /// 現在のステート
        /// </summary>
        public UState? CurrentState { get; private set; }

        /// <summary>
        /// 次のステート
        /// </summary>
        public UState? NextState { get; private set; }

        /// <summary>
        /// エラーハンドラー
        /// </summary>
        public event Action<Exception>? ExceptionHandler;

        /// <summary>
        /// エラーハンドリングのモード
        /// </summary>
        public UnHandleExceptionMode UnHandleException { get; private set; }

        /// <summary>
        /// ステートマシンの状態
        /// </summary>
        public MachineState State { get; private set; }

        /// <summary>
        /// 起動中かどうか
        /// </summary>
        public bool Running => CurrentState != null;
        #endregion

        /// <summary>
        /// コンストラクタ
        /// ステートマシーンの初期化
        /// </summary>
        /// <param name="Context"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public UStateMachine(TContext Context)
        {
            this.Context = Context ?? throw new ArgumentNullException(nameof(Context));
            stateList = new Dictionary<Type, UState>();
            CurrentState = null;
            NextState = null;
            State = MachineState.Idle;
            UnHandleException = UnHandleExceptionMode.ThrowException;
        }

        /// <summary>
        /// ステートマシーンへのステートの登録
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <exception cref="UStateAlreadyRunningException"></exception>
        /// <exception cref="UStateAlreadyRegisterException"></exception>
        public void RegisterState<T>() where T : UState, new()
        {
            var typeInfo = typeof(T);
            if (Running)
            {
                throw new UStateAlreadyRunningException("StateMachine is Already Running.");
            }
            else if (stateList.ContainsKey(typeInfo))
            {
                throw new UStateAlreadyRegisterException("State is Already Registered.");
            }
            GetStateOrCreate<T>();
        }

        /// <summary>
        /// ステートマシーンへ登録されているステートの解除
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <exception cref="UStateAlreadyRunningException"></exception>
        /// <exception cref="UStateNotFoundStateException"></exception>
        public void UnRegisterState<T>() where T : UState, new()
        {
            var typeInfo = typeof(T);
            if (Running)
            {
                throw new UStateAlreadyRunningException("StateMachine is Already Running.");
            }
            else if (!stateList.ContainsKey(typeInfo))
            {
                throw new UStateNotFoundStateException("State is Not Found.");
            }
            stateList.Remove(typeof(T));
        }

        public void Update()
        {
            if (State != MachineState.Idle)
            {
                // 多重にUpdateをしてしまうならエラー
                var currentThreadID = Environment.CurrentManagedThreadId;
                throw lastThreadID != currentThreadID ?
                    new InvalidOperationException($"Used in another thread. [UpdateThread={lastThreadID}, CurrentThread={currentThreadID}]") :
                    new InvalidOperationException("StateMachine is Already Update.");
            }

            // スレッドIDの取得
            lastThreadID = Environment.CurrentManagedThreadId;

            // ステートマシンが実行されてなかったら開始処理実行
            if (!Running) StartUpStateMachine();
            if (CurrentState == null) throw new UStateNotFoundStateException("CurrentState is null");

            // ステートの更新
            try
            {
                if (NextState == null)
                {
                    State = MachineState.Update;
                    CurrentState.Update();
                }

                // 次のステートへ推移
                while (NextState != null)
                {
                    State = MachineState.Exit;
                    CurrentState.Exit();

                    CurrentState = NextState;
                    NextState = null;

                    State = MachineState.Entry;
                    CurrentState.Entry();
                }

                State = MachineState.Idle;
            }
            catch (Exception ex)
            {
                State = MachineState.Idle;
                HandleException(ex);
                return;
            }
        }

        /// <summary>
        /// 開始時の処理
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        private void StartUpStateMachine()
        {
            // 開始ステートが設定されているか
            if (NextState == null)
            {
                throw new InvalidOperationException("Please Set The Starting State.");
            }

            CurrentState = NextState;
            NextState = null;

            try
            {
                State = MachineState.Entry;
                CurrentState.Entry();
            }
            catch (Exception ex)
            {
                // 現在のステートをnullに設定
                NextState = CurrentState;
                CurrentState = null;
                // アイルドル状態ににしてハンドラに投げる
                State = MachineState.Idle;
                HandleException(ex);
                return;
            }

            if (NextState == null)
            {
                State = MachineState.Idle;
                return;
            }
        }

        /// <summary>
        /// 開始時のステートを設定
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <exception cref="UStateAlreadyRunningException"></exception>
        public void SetStartState<T>() where T : UState, new()
        {
            if (Running)
            {
                throw new UStateAlreadyRunningException("StateMachine is Already Running.");
            }
            else if (!stateList.TryGetValue(typeof(T), out var state))
            {
                throw new UStateNotFoundStateException("State is Not Found.");
            }
            else
            {
                NextState = state;
            }
        }


        /// <summary>
        /// エラーハンドリング
        /// </summary>
        /// <param name="exception"></param>
        private void HandleException(Exception exception)
        {
            bool handle = UnHandleException switch
            {
                UnHandleExceptionMode.CatchException => true,
                UnHandleExceptionMode.ThrowException => false,
                _ => true,
            };

            if (handle)
            {
                ExceptionHandler?.Invoke(exception);
            }
            else
            {
                ExceptionDispatchInfo.Capture(exception).Throw();
            }
        }

        /// <summary>
        /// ステートを取得
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        private UState GetStateOrCreate<T>() where T : UState, new()
        {
            var typeInfo = typeof(T);
            if (!stateList.TryGetValue(typeInfo, out var state))
            {
                state = new T
                {
                    Context = Context,
                    StateMachine = this
                };
                stateList.Add(typeInfo, state);
            }
            return state;
        }
    }


    /// <summary>
    /// 例外ベース
    /// </summary>
    public class UStateBaseException : Exception
    {
        public UStateBaseException() : base() { }

        public UStateBaseException(string? message) : base(message) { }
    }

    public class UStateAlreadyRunningException : UStateBaseException
    {
        public UStateAlreadyRunningException() : base() { }

        public UStateAlreadyRunningException(string? message) : base(message) { }
    }

    public class UStateAlreadyRegisterException : UStateBaseException
    {
        public UStateAlreadyRegisterException() : base() { }

        public UStateAlreadyRegisterException(string? message) : base(message) { }
    }

    public class UStateNotFoundStateException : UStateBaseException
    {
        public UStateNotFoundStateException() : base() { }

        public UStateNotFoundStateException(string? message) : base(message) { }
    }
}
