using UnityEngine;
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.ShaderGraph.Drawing;

namespace UnityEditor.ShaderGraph
{
    class ShaderGraphAssetPostProcessor : AssetPostprocessor
    {
        static void RegisterShaders(string[] paths)
        {
            foreach (var path in paths)
            {
                if (!path.EndsWith(ShaderGraphImporter.Extension, StringComparison.InvariantCultureIgnoreCase))
                    continue;

                var mainObj = AssetDatabase.LoadMainAssetAtPath(path);
                if (mainObj is Shader)
                    ShaderUtil.RegisterShader((Shader)mainObj);

                var objs = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
                foreach (var obj in objs)
                {
                    if (obj is Shader)
                        ShaderUtil.RegisterShader((Shader)obj);
                }
            }
        }

        static void UpdateAfterAssetChange(string[] newNames)
        {
            // This will change the title of the window.
            MaterialGraphEditWindow[] windows = Resources.FindObjectsOfTypeAll<MaterialGraphEditWindow>();
            foreach (var matGraphEditWindow in windows)
            {
                for (int i = 0; i < newNames.Length; ++i)
                {
                    if (matGraphEditWindow.selectedGuid == AssetDatabase.AssetPathToGUID(newNames[i]))
                        matGraphEditWindow.UpdateTitle();
                }
            }
        }

        static void DisplayDeletionDialog(string[] deletedAssets)
        {
            MaterialGraphEditWindow[] windows = Resources.FindObjectsOfTypeAll<MaterialGraphEditWindow>();
            foreach (var matGraphEditWindow in windows)
            {
                for (int i = 0; i < deletedAssets.Length; ++i)
                {
                    if (matGraphEditWindow.selectedGuid == AssetDatabase.AssetPathToGUID(deletedAssets[i]))
                        matGraphEditWindow.AssetWasDeleted();
                }
            }
        }

        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            RegisterShaders(importedAssets);

            // Moved assets
            bool anyMovedShaders = movedAssets.Any(val => val.EndsWith(ShaderGraphImporter.Extension, StringComparison.InvariantCultureIgnoreCase));
            anyMovedShaders |= movedAssets.Any(val => val.EndsWith(ShaderSubGraphImporter.Extension, StringComparison.InvariantCultureIgnoreCase));
            if (anyMovedShaders)
                UpdateAfterAssetChange(movedAssets);

            // Deleted assets
            bool anyRemovedShaders = deletedAssets.Any(val => val.EndsWith(ShaderGraphImporter.Extension, StringComparison.InvariantCultureIgnoreCase));
            anyRemovedShaders |= deletedAssets.Any(val => val.EndsWith(ShaderSubGraphImporter.Extension, StringComparison.InvariantCultureIgnoreCase));
            if (anyRemovedShaders)
                DisplayDeletionDialog(deletedAssets);

            var changedGraphGuids = importedAssets
                .Where(x => x.EndsWith(ShaderGraphImporter.Extension, StringComparison.InvariantCultureIgnoreCase)
                || x.EndsWith(ShaderSubGraphImporter.Extension, StringComparison.InvariantCultureIgnoreCase))
                .Select(AssetDatabase.AssetPathToGUID)
                .ToList();

            MaterialGraphEditWindow[] windows = null;
            if (changedGraphGuids.Count > 0)
            {
                windows = Resources.FindObjectsOfTypeAll<MaterialGraphEditWindow>();
                foreach (var window in windows)
                {
                    if (changedGraphGuids.Contains(window.selectedGuid))
                    {
                        window.CheckForChanges();
                    }
                }
            }

            // moved or imported subgraphs or HLSL files should notify open shadergraphs that they need to handle them
            var changedFileGUIDs = movedAssets.Concat(importedAssets).Concat(deletedAssets)
                .Where(x => x.EndsWith(ShaderSubGraphImporter.Extension, StringComparison.InvariantCultureIgnoreCase)
                    || CustomFunctionNode.s_ValidExtensions.Contains(Path.GetExtension(x)))
                .Select(AssetDatabase.AssetPathToGUID)
                .Distinct()
                .ToList();

            if (changedFileGUIDs.Count > 0)
            {
                if (windows == null)
                {
                    windows = Resources.FindObjectsOfTypeAll<MaterialGraphEditWindow>();
                }
                foreach (var window in windows)
                {
                    window.ReloadSubGraphsOnNextUpdate(changedFileGUIDs);
                }
            }
        }
    }
}
