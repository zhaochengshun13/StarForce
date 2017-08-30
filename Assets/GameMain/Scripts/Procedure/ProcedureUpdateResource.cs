using GameFramework;
using GameFramework.Event;
using System.Collections.Generic;
using UnityEngine;
using UnityGameFramework.Runtime;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

namespace StarForce
{
    public class ProcedureUpdateResource : ProcedureBase
    {
        private bool m_UpdateAllComplete = false;
        private int m_UpdateCount = 0;
        private int m_UpdateTotalZipLength = 0;
        private int m_UpdateSuccessCount = 0;
        private List<UpdateLengthData> m_UpdateLengthData = new List<UpdateLengthData>();

        public override bool UseNativeDialog
        {
            get
            {
                return true;
            }
        }

        protected override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);

            m_UpdateAllComplete = false;
            m_UpdateCount = 0;
            m_UpdateTotalZipLength = 0;
            m_UpdateSuccessCount = 0;
            m_UpdateLengthData.Clear();

            GameEntry.Event.Subscribe(UnityGameFramework.Runtime.EventId.ResourceCheckComplete, OnResourceCheckComplete);
            GameEntry.Event.Subscribe(UnityGameFramework.Runtime.EventId.ResourceUpdateStart, OnResourceUpdateStart);
            GameEntry.Event.Subscribe(UnityGameFramework.Runtime.EventId.ResourceUpdateChanged, OnResourceUpdateChanged);
            GameEntry.Event.Subscribe(UnityGameFramework.Runtime.EventId.ResourceUpdateSuccess, OnResourceUpdateSuccess);
            GameEntry.Event.Subscribe(UnityGameFramework.Runtime.EventId.ResourceUpdateFailure, OnResourceUpdateFailure);
            GameEntry.Event.Subscribe(UnityGameFramework.Runtime.EventId.ResourceUpdateAllComplete, OnResourceUpdateAllComplete);

            GameEntry.Resource.CheckResources();
        }

        protected override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
        {
            GameEntry.Event.Unsubscribe(UnityGameFramework.Runtime.EventId.ResourceCheckComplete, OnResourceCheckComplete);
            GameEntry.Event.Unsubscribe(UnityGameFramework.Runtime.EventId.ResourceUpdateStart, OnResourceUpdateStart);
            GameEntry.Event.Unsubscribe(UnityGameFramework.Runtime.EventId.ResourceUpdateChanged, OnResourceUpdateChanged);
            GameEntry.Event.Unsubscribe(UnityGameFramework.Runtime.EventId.ResourceUpdateSuccess, OnResourceUpdateSuccess);
            GameEntry.Event.Unsubscribe(UnityGameFramework.Runtime.EventId.ResourceUpdateFailure, OnResourceUpdateFailure);
            GameEntry.Event.Unsubscribe(UnityGameFramework.Runtime.EventId.ResourceUpdateAllComplete, OnResourceUpdateAllComplete);

            base.OnLeave(procedureOwner, isShutdown);
        }

        protected override void OnUpdate(ProcedureOwner procedureOwner, float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(procedureOwner, elapseSeconds, realElapseSeconds);

            if (!m_UpdateAllComplete)
            {
                return;
            }

            ChangeState<ProcedurePreload>(procedureOwner);
        }

        private void StartUpdateResources(object userData)
        {
            GameEntry.Resource.UpdateResources();
            Log.Info("Start update resources...");
        }

        private void OnResourceCheckComplete(object sender, GameEventArgs e)
        {
            ResourceCheckCompleteEventArgs ne = (ResourceCheckCompleteEventArgs)e;
            Log.Info("Check resource complete, '{0}' resources need to update, zip length is '{1}', unzip length is '{2}'.", ne.UpdateCount.ToString(), ne.UpdateTotalZipLength.ToString(), ne.UpdateTotalLength.ToString());

            m_UpdateCount = ne.UpdateCount;
            m_UpdateTotalZipLength = ne.UpdateTotalZipLength;
            if (m_UpdateCount <= 0)
            {
                ProcessUpdateAllComplete();
                return;
            }

            if (Application.internetReachability == NetworkReachability.ReachableViaCarrierDataNetwork)
            {
                GameEntry.UI.OpenDialog(new DialogParams
                {
                    Mode = 2,
                    Title = GameEntry.Localization.GetString("UpdateResourceViaCarrierDataNetwork.Title"),
                    Message = GameEntry.Localization.GetString("UpdateResourceViaCarrierDataNetwork.Message"),
                    ConfirmText = GameEntry.Localization.GetString("UpdateResourceViaCarrierDataNetwork.UpdateButton"),
                    OnClickConfirm = StartUpdateResources,
                    CancelText = GameEntry.Localization.GetString("UpdateResourceViaCarrierDataNetwork.QuitButton"),
                    OnClickCancel = delegate (object userData) { UnityGameFramework.Runtime.GameEntry.Shutdown(ShutdownType.Quit); },
                });

                return;
            }

            StartUpdateResources(null);
        }

        private void OnResourceUpdateStart(object sender, GameEventArgs e)
        {
            ResourceUpdateStartEventArgs ne = (ResourceUpdateStartEventArgs)e;

            for (int i = 0; i < m_UpdateLengthData.Count; i++)
            {
                if (m_UpdateLengthData[i].Name == ne.Name)
                {
                    Log.Warning("Update resource '{0}' is invalid.", ne.Name);
                    m_UpdateLengthData[i].Length = 0;
                    return;
                }
            }

            m_UpdateLengthData.Add(new UpdateLengthData(ne.Name));
        }

        private void OnResourceUpdateChanged(object sender, GameEventArgs e)
        {
            ResourceUpdateChangedEventArgs ne = (ResourceUpdateChangedEventArgs)e;

            for (int i = 0; i < m_UpdateLengthData.Count; i++)
            {
                if (m_UpdateLengthData[i].Name == ne.Name)
                {
                    m_UpdateLengthData[i].Length = ne.CurrentLength;
                    return;
                }
            }

            Log.Warning("Update resource '{0}' is invalid.", ne.Name);
        }

        private void OnResourceUpdateSuccess(object sender, GameEventArgs e)
        {
            ResourceUpdateSuccessEventArgs ne = (ResourceUpdateSuccessEventArgs)e;
            Log.Info("Update resource '{0}' success.", ne.Name);

            for (int i = 0; i < m_UpdateLengthData.Count; i++)
            {
                if (m_UpdateLengthData[i].Name == ne.Name)
                {
                    m_UpdateLengthData[i].Length = ne.ZipLength;
                    m_UpdateSuccessCount++;
                    return;
                }
            }

            Log.Warning("Update resource '{0}' is invalid.", ne.Name);
        }

        private void OnResourceUpdateFailure(object sender, GameEventArgs e)
        {
            ResourceUpdateFailureEventArgs ne = (ResourceUpdateFailureEventArgs)e;
            if (ne.RetryCount >= ne.TotalRetryCount)
            {
                Log.Error("Update resource '{0}' failure from '{1}' with error message '{2}', retry count '{3}'.", ne.Name, ne.DownloadUri, ne.ErrorMessage, ne.RetryCount.ToString());
                return;
            }
            else
            {
                Log.Info("Update resource '{0}' failure from '{1}' with error message '{2}', retry count '{3}'.", ne.Name, ne.DownloadUri, ne.ErrorMessage, ne.RetryCount.ToString());
            }

            for (int i = 0; i < m_UpdateLengthData.Count; i++)
            {
                if (m_UpdateLengthData[i].Name == ne.Name)
                {
                    m_UpdateLengthData.Remove(m_UpdateLengthData[i]);
                    return;
                }
            }

            Log.Warning("Update resource '{0}' is invalid.", ne.Name);
        }

        private void OnResourceUpdateAllComplete(object sender, GameEventArgs e)
        {
            Log.Info("All resources update complete.");
            ProcessUpdateAllComplete();
        }

        private void ProcessUpdateAllComplete()
        {
            m_UpdateAllComplete = true;
        }

        private class UpdateLengthData
        {
            private readonly string m_Name;

            public UpdateLengthData(string name)
            {
                m_Name = name;
            }

            public string Name
            {
                get
                {
                    return m_Name;
                }
            }

            public int Length
            {
                get;
                set;
            }
        }
    }
}
