using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

namespace UStateMachine
{
    public sealed class UStateMachine
    {
        public abstract class UState
        {
            private readonly HashSet<Type> destination = new();

            protected internal virtual void Entry() { }
            protected internal virtual void Update() { }
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
            None,
            Enter,
            Update,
            Exit
        }

        #region StateMachine MemberVariables
        /// <summary>
        /// 登録されたステート一覧
        /// </summary>
        private readonly Dictionary<Type, UState> stateList;
        #endregion

        #region StateMachine Propertys
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
        public event Func<UStateBaseException, bool> ExceptionHandler;

        /// <summary>
        /// エラーハンドリングのモード
        /// </summary>
        public UnHandleExceptionMode UnHandleException { get; private set; }

        /// <summary>
        /// ステートマシンの状態
        /// </summary>
        public MachineState State { get; private set; }
        #endregion

        public UStateMachine()
        {
            stateList = new Dictionary<Type, UState>();
            CurrentState = null;
            NextState = null;
            ExceptionHandler = (_) => false;
            State = MachineState.None;
            UnHandleException = UnHandleExceptionMode.ThrowException;
        }

        public void RegisterState<T>() where T : UState, new()
        {
            var typeInfo = typeof(T);
            if (State != MachineState.None)
            {
                throw new UStateAlreadyRunningException("StateMachine is Already Running.");
            }
            else if (stateList.ContainsKey(typeInfo))
            {
                throw new UStateAlreadyRegisterException("State is Already Registered.");
            }

            stateList.Add(typeInfo, new T());
        }

        public void UnRegisterState<T>() where T : UState, new()
        {
            var typeInfo = typeof(T);
            if (State != MachineState.None)
            {
                throw new UStateAlreadyRunningException("StateMachine is Already Running.");
            }
            else if (!stateList.ContainsKey(typeInfo))
            {
                throw new UStateNotFoundStateException("State is Not Found.");
            }
            stateList.Remove(typeof(T));
        }

        private void Error(UStateBaseException exception)
        {
            var error = UnHandleException switch
            {
                UnHandleExceptionMode.CatchException => ExceptionHandler?.Invoke(exception),
                UnHandleExceptionMode.ThrowException => false,
                _ => false,
            };

            if (error != null || !error == true) return;
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

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
