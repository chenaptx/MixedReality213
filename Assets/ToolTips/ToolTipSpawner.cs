﻿//
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
//

using System;
using System.Collections;
using UnityEngine;

namespace MRDL.ToolTips
{
    public enum TipVanishBehaviorEnum
    {
        Manual,
        VanishOnFocusExit,
        VanishOnTap,
    }

    public enum TipAppearBehaviorEnum
    {
        Manual,
        AppearOnFocusEnter,
        AppearOnTap,
    }

    public enum TipRemainBehaviorEnum
    {
        Indefinite,
        Timeout,
    }

    [Serializable]
    public class TipSpawnSettings : TipConnectionSettings
    {
        public bool IsEmpty
        {
            get { return string.IsNullOrEmpty(Text); }
        }

        public bool IsInstantiated
        {
            get { return InstantiatedToolTip != null; }
        }
        
        [Header("Content")]
        public string Text;
        [Header("Visibility settings")]
        public TipAppearBehaviorEnum AppearBehavior = TipAppearBehaviorEnum.Manual;
        public TipVanishBehaviorEnum VanishBehavior = TipVanishBehaviorEnum.Manual;
        public TipRemainBehaviorEnum RemainBehavior = TipRemainBehaviorEnum.Indefinite;
        [Range(0f, 5f)]
        public float AppearDelay = 1.25f;
        [Range(0f, 5f)]
        public float VanishDelay = 2f;

        public ToolTip InstantiatedToolTip;
    }

    /// <summary>
    /// Add to any InteractibleObject to spawn ToolTips on tap or on focus, according to preference
    /// Applies its follow settings to the spawned ToolTip's ToolTipConnector component
    /// </summary>
    public class ToolTipSpawner : MonoBehaviour
    {
        public TipSpawnSettings Settings;

        public GameObject ToolTipPrefab;

        private float focusEnterTime = 0f;
        private float focusExitTime = 0f;
        private float tappedTime = 0f;
        private float targetDisappearTime = Mathf.Infinity;
        private bool hasFocus;

        public void Tapped()
        {
            tappedTime = Time.unscaledTime;
            if (!Settings.IsInstantiated || !Settings.InstantiatedToolTip.gameObject.activeSelf)
            {
                switch (Settings.AppearBehavior)
                {
                    case TipAppearBehaviorEnum.AppearOnTap:
                        ShowToolTip();
                        return;

                    default:
                        break;
                }
            }
        }

        public void FocusEnter()
        {
            focusEnterTime = Time.unscaledTime;
            hasFocus = true;
            if (!Settings.IsInstantiated || !Settings.InstantiatedToolTip.gameObject.activeSelf)
            {
                switch (Settings.AppearBehavior)
                {
                    case TipAppearBehaviorEnum.AppearOnFocusEnter:
                        ShowToolTip();
                        break;

                    default:
                        break;
                }
            }
        }

        public void FocusExit()
        {
            focusExitTime = Time.unscaledTime;
            hasFocus = false;
        }

        public void HideToolTip()
        {
            if (!Settings.IsInstantiated)
            {
                Debug.Log("Instantiating tooltip");
                GameObject toolTipGo = GameObject.Instantiate(ToolTipPrefab) as GameObject;
                ToolTip toolTip = toolTipGo.GetComponent<ToolTip>();
                toolTip.transform.position = transform.position;
                toolTip.transform.parent = transform;
                toolTip.gameObject.SetActive(false);
                Settings.InstantiatedToolTip = toolTip;
            }
        }

        public void ShowToolTip()
        {
            Debug.Log("Showing tool tip");
            StartCoroutine(UpdateTooltip(focusEnterTime, tappedTime));
        }

        private IEnumerator UpdateTooltip(float focusEnterTimeOnStart, float tappedTimeOnStart)
        {
            if (!Settings.IsInstantiated)
            {
                Debug.Log("Instantiating tooltip");
                GameObject toolTipGo = GameObject.Instantiate(ToolTipPrefab) as GameObject;
                ToolTip toolTip = toolTipGo.GetComponent<ToolTip>();                
                toolTip.transform.position = transform.position;
                toolTip.transform.parent = transform;
                toolTip.gameObject.SetActive(false);
                Settings.InstantiatedToolTip = toolTip;
            }

            switch (Settings.AppearBehavior)
            {
                case TipAppearBehaviorEnum.AppearOnFocusEnter:
                    // Wait for the appear delay
                    yield return new WaitForSeconds(Settings.AppearDelay);
                    // If we don't have focus any more, get out of here
                    if (!hasFocus)
                    {
                        yield break;
                    }
                    break;
            }

            // wait one tick
            yield return null;

            Debug.Log("Setting to active...");

            ToolTipConnector connector = Settings.InstantiatedToolTip.GetComponent<ToolTipConnector>();
            if (connector == null)
                connector = Settings.InstantiatedToolTip.gameObject.AddComponent<ToolTipConnector>();

            connector.Settings = Settings;

            Settings.InstantiatedToolTip.gameObject.SetActive(true);
            Settings.InstantiatedToolTip.ToolTipText = Settings.Text;

            if (Settings.PivotMode == TipPivotModeEnum.ManualPosition)
                Settings.InstantiatedToolTip.PivotPosition = transform.TransformPoint(Settings.ManualPivotLocalPosition);

            while (Settings.InstantiatedToolTip.gameObject.activeSelf)
            {
                Debug.Log("Still active...");
                //check whether we're suppose to disappear
                switch (Settings.VanishBehavior)
                {
                    case TipVanishBehaviorEnum.Manual:
                        break;

                    case TipVanishBehaviorEnum.VanishOnFocusExit:
                        if (!hasFocus)
                        {
                            if (Time.time - focusExitTime > Settings.VanishDelay)
                            {
                                Debug.Log("Setting tooltip inactive");
                                Settings.InstantiatedToolTip.gameObject.SetActive(false);
                            }
                        }
                        break;

                    case TipVanishBehaviorEnum.VanishOnTap:
                        if (tappedTime != tappedTimeOnStart)
                        {
                            Debug.Log("Setting tooltip inactive");
                            Settings.InstantiatedToolTip.gameObject.SetActive(false);
                        }
                        break;
                }
                yield return null;
            }

            Debug.Log("Tool tip no longer active, ending update");

            yield break;
        }
        
        private void OnDrawGizmos()
        {
            if (Application.isPlaying)
                return;

            Gizmos.color = Color.cyan;
            Transform relativeTo = null;
            switch (Settings.PivotDirectionOrient)
            {
                case TipOrientTypeEnum.OrientToCamera:
                    relativeTo = Camera.main.transform;//Veil.Instance.HeadTransform;
                    break;

                case TipOrientTypeEnum.OrientToObject:
                    relativeTo = (Settings.Target != null) ? Settings.Target.transform : transform;
                    break;
            }

            if (Settings.PivotMode == TipPivotModeEnum.Automatic)
            {
                Vector3 targetPosition = (Settings.Target != null) ? Settings.Target.transform.position : transform.position;
                Vector3 toolTipPosition = targetPosition + ToolTipConnector.GetDirectionFromPivotDirection(
                                Settings.PivotDirection,
                                Settings.ManualPivotDirection,
                                relativeTo) * Settings.PivotDistance;
                Gizmos.DrawLine(targetPosition, toolTipPosition);
                Gizmos.DrawWireCube(toolTipPosition, Vector3.one * 0.05f);
            }
            else
            {
                Vector3 targetPosition = (Settings.Target != null) ? Settings.Target.transform.position : transform.position;
                Vector3 toolTipPosition = transform.TransformPoint(Settings.ManualPivotLocalPosition);
                Gizmos.DrawLine(targetPosition, toolTipPosition);
                Gizmos.DrawWireCube(toolTipPosition, Vector3.one * 0.05f);
            }
        }
    }
}
