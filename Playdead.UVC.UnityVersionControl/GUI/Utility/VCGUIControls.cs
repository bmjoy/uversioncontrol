// Copyright (c) <2017> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>

using System.IO;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace VersionControl.UserInterface
{
    using Extensions;
    public static class VCGUIControls
    {
        private static GUIStyle GetPrefabToolbarStyle(GUIStyle style, bool vcRelated)
        {
            var vcStyle = new GUIStyle(style);
            if (vcRelated)
            {
                vcStyle.fontStyle = FontStyle.Bold;
            }
            return vcStyle;
        }

        public static void VersionControlStatusGUI(GUIStyle style, VersionControlStatus assetStatus, Object obj, bool showAddCommit, bool showLockAndAllowLocalEdit, bool showRevert, bool confirmRevert = false)
        {
            using (new PushState<bool>(GUI.enabled, VCCommands.Instance.Ready, v => GUI.enabled = v))
            {
                if (assetStatus.lockStatus == VCLockStatus.LockedHere || assetStatus.ModifiedOrLocalEditAllowed() || !VCUtility.ManagedByRepository(assetStatus))
                {
                    if (!assetStatus.ModifiedOrLocalEditAllowed() && obj.GetAssetPath() != "" && showAddCommit)
                    {
                        if (GUILayout.Button((VCUtility.ManagedByRepository(assetStatus) ? Terminology.commit : Terminology.add), GetPrefabToolbarStyle(style, true)))
                        {
                            VCUtility.ApplyAndCommit(obj, Terminology.commit + " from Inspector");
                        }
                    }
                }

                if (!VCUtility.HaveVCLock(assetStatus) && VCUtility.ManagedByRepository(assetStatus) && showLockAndAllowLocalEdit)
                {
                    if (assetStatus.fileStatus == VCFileStatus.Added)
                    {
                        if (GUILayout.Button(Terminology.commit, GetPrefabToolbarStyle(style, true)))
                        {
                            VCUtility.ApplyAndCommit(obj, Terminology.commit + " from Inspector");
                        }
                    }
                    else if (assetStatus.lockStatus != VCLockStatus.LockedOther)
                    {
                        if (GUILayout.Button(Terminology.getlock, GetPrefabToolbarStyle(style, true)))
                        {
                            VCCommands.Instance.GetLockTask(obj.ToAssetPaths());
                        }
                    }
                    if (!assetStatus.LocalEditAllowed())
                    {
                        if (GUILayout.Button(Terminology.allowLocalEdit, GetPrefabToolbarStyle(style, true)))
                        {
                            VCCommands.Instance.AllowLocalEdit(obj.ToAssetPaths());
                        }
                    }
                }

                if (showRevert)
                {
                    if (GUILayout.Button(Terminology.revert, GetPrefabToolbarStyle(style, VCUtility.ShouldVCRevert(obj))))
                    {
                        if ((!confirmRevert || Event.current.shift) || VCUtility.VCDialog(Terminology.revert, obj))
                        {
                            var seletedGo = Selection.activeGameObject;
                            var revertedObj = VCUtility.Revert(obj);
                            OnNextUpdate.Do(() => Selection.activeObject = ((obj is GameObject) ? revertedObj : seletedGo));
                        }
                    }
                }
            }
        }



        public static GUIStyle GetVCBox(VersionControlStatus assetStatus)
        {
            return new GUIStyle(GUI.skin.box)
            {
                border = new RectOffset(2, 2, 2, 2),
                padding = new RectOffset(1, 1, 1, 1),
                normal = { background = IconUtils.boxIcon.GetTexture(AssetStatusUtils.GetStatusColor(assetStatus, true)) }
            };
        }

        public static GUIStyle GetLockStatusStyle()
        {
            return new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.black }, alignment = TextAnchor.MiddleCenter };
        }

        public static void CreateVCContextMenu(ref GenericMenu menu, IEnumerable<string> assetPaths)
        {
            menu.AddItem(new GUIContent(Terminology.add), false, () => VCCommands.Instance.Add(assetPaths));
            menu.AddItem(new GUIContent(Terminology.getlock), false, () => VCCommands.Instance.GetLock(assetPaths));
            menu.AddItem(new GUIContent(Terminology.commit), false, () => VCCommands.Instance.CommitDialog(assetPaths));
            menu.AddItem(new GUIContent(Terminology.revert), false, () => VCCommands.Instance.Revert(assetPaths));
            menu.AddItem(new GUIContent(Terminology.delete), false, () => VCCommands.Instance.Delete(assetPaths));
        }

        public struct ValidActions
        {
            public bool showAdd, showOpen, showDiff, showCommit, showRevert, showDelete, showOpenLocal, showUnlock, showUpdate, showForceOpen, showDisconnect;
        }

        public static ValidActions GetValidActions(string assetPath, Object instance = null)
        {
            var assetStatus = VCCommands.Instance.GetAssetStatus(assetPath);

            ValidActions validActions;
            bool isPrefab = instance != null && PrefabHelper.IsPrefab(instance);
            bool isPrefabParent = isPrefab && PrefabHelper.IsPrefabParent(instance);
            bool isFolder = Directory.Exists(assetPath);
            bool diffableAsset = VCUtility.IsDiffableAsset(assetPath);
            bool mergableAsset = VCUtility.IsMergableAsset(assetPath);
            bool modifiedDiffableAsset = diffableAsset && assetStatus.fileStatus != VCFileStatus.Normal;
            bool modifiedMeta = assetStatus.MetaStatus().fileStatus != VCFileStatus.Normal;
            bool lockedMeta = assetStatus.MetaStatus().lockStatus == VCLockStatus.LockedHere;
            bool modified = assetStatus.fileStatus == VCFileStatus.Modified;
            bool deleted = assetStatus.fileStatus == VCFileStatus.Deleted;
            bool added = assetStatus.fileStatus == VCFileStatus.Added;
            bool unversioned = assetStatus.fileStatus == VCFileStatus.Unversioned;
            bool ignored = assetStatus.fileStatus == VCFileStatus.Ignored;
            bool replaced = assetStatus.fileStatus == VCFileStatus.Replaced;
            bool lockedByOther = assetStatus.lockStatus == VCLockStatus.LockedOther;
            bool managedByRep = VCUtility.ManagedByRepository(assetStatus);
            bool haveControl = VCUtility.HaveAssetControl(assetStatus);
            bool haveLock = VCUtility.HaveVCLock(assetStatus);
            bool allowLocalEdit = assetStatus.LocalEditAllowed();
            bool pending = assetStatus.reflectionLevel == VCReflectionLevel.Pending;

            validActions.showAdd        = !pending && !ignored && unversioned;
            validActions.showOpen       = !pending && !validActions.showAdd && !added && !haveLock && !deleted && !isFolder && !mergableAsset && (!lockedByOther || allowLocalEdit);
            validActions.showDiff       = !pending && !ignored && !deleted && modifiedDiffableAsset && managedByRep;
            validActions.showCommit     = !pending && !ignored && !allowLocalEdit && (haveLock || added || deleted || modifiedDiffableAsset || isFolder || modifiedMeta);
            validActions.showRevert     = !pending && !ignored && !unversioned && (haveControl || modified || added || deleted || replaced || modifiedDiffableAsset || modifiedMeta || lockedMeta);
            validActions.showDelete     = !pending && !ignored && !deleted && !lockedByOther;
            validActions.showOpenLocal  = !pending && !ignored && !deleted && !isFolder && !allowLocalEdit && !unversioned && !added && !haveLock && !mergableAsset;
            validActions.showUnlock     = !pending && !ignored && !allowLocalEdit && haveLock;
            validActions.showUpdate     = !pending && !ignored && !added && managedByRep && instance != null;
            validActions.showForceOpen  = !pending && !ignored && !deleted && !isFolder && !allowLocalEdit && !unversioned && !added && lockedByOther && Event.current.shift;
            validActions.showDisconnect = isPrefab && !isPrefabParent;

            return validActions;
        }

        public static void CreateVCContextMenu(ref GenericMenu menu, string assetPath, Object instance = null)
        {
            if (VCUtility.ValidAssetPath(assetPath))
            {
                bool ready = VCCommands.Instance.Ready;
                if (ready)
                {
                    if (instance && ObjectUtilities.ChangesStoredInScene(instance)) assetPath = SceneManagerUtilities.GetCurrentScenePath();
                    var validActions = GetValidActions(assetPath, instance);                    

                    if (validActions.showDiff)      menu.AddItem(new GUIContent(Terminology.diff),              false, () => VCUtility.DiffWithBase(assetPath));
                    if (validActions.showAdd)       menu.AddItem(new GUIContent(Terminology.add),               false, () => VCCommands.Instance.Add(new[] { assetPath }));
                    if (validActions.showOpen)      menu.AddItem(new GUIContent(Terminology.getlock),           false, () => GetLock(assetPath, instance));
                    if (validActions.showOpenLocal) menu.AddItem(new GUIContent(Terminology.allowLocalEdit),    false, () => AllowLocalEdit(assetPath, instance));
                    if (validActions.showForceOpen) menu.AddItem(new GUIContent("Force " + Terminology.getlock),false, () => GetLock(assetPath, instance, OperationMode.Force));
                    if (validActions.showCommit)    menu.AddItem(new GUIContent(Terminology.commit),            false, () => Commit(assetPath, instance));
                    if (validActions.showUnlock)    menu.AddItem(new GUIContent(Terminology.unlock),            false, () => VCCommands.Instance.ReleaseLock(new[] { assetPath }));
                    if (validActions.showDisconnect)menu.AddItem(new GUIContent("Disconnect"),                  false, () => PrefabHelper.DisconnectPrefab(instance as GameObject));
                    if (validActions.showDelete)    menu.AddItem(new GUIContent(Terminology.delete),            false, () => VCCommands.Instance.Delete(new[] { assetPath }));
                    if (validActions.showRevert)    menu.AddItem(new GUIContent(Terminology.revert),            false, () => Revert(assetPath, instance));
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("..Busy.."));
                }
            }
        }

        private static void GetLock(string assetPath, Object instance, OperationMode operationMode = OperationMode.Normal)
        {
            if (instance != null) VCUtility.GetLock(instance, operationMode);
            else VCCommands.Instance.GetLock(new[] { assetPath }, operationMode);
        }

        private static void AllowLocalEdit(string assetPath, Object instance)
        {
            if (instance != null) VCUtility.AllowLocalEdit(instance);
            else VCCommands.Instance.AllowLocalEdit(new[] { assetPath });
        }

        private static void Commit(string assetPath, Object instance)
        {
            if (instance != null) VCUtility.ApplyAndCommit(instance);
            else VCCommands.Instance.CommitDialog(new[] { assetPath });
        }

        private static void Revert(string assetPath, Object instance)
        {
            if (instance != null) VCUtility.Revert(instance);
            else VCCommands.Instance.Revert(new[] { assetPath });
        }

        public static void DiaplayVCContextMenu(string assetPath, Object instance = null, float xoffset = 0.0f, float yoffset = 0.0f, bool showAssetName = false)
        {
            var menu = new GenericMenu();
            if (showAssetName)
            {
                menu.AddDisabledItem(new GUIContent(Path.GetFileName(assetPath)));
                menu.AddSeparator("");
            }
            CreateVCContextMenu(ref menu, assetPath, instance);
            menu.DropDown(new Rect(Event.current.mousePosition.x + xoffset, Event.current.mousePosition.y + yoffset, 0.0f, 0.0f));
            Event.current.Use();
        }
    }
}

