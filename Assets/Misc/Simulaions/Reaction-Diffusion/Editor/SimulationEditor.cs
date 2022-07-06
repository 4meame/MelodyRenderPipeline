using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(RDSimulation))]
public class SimulationEditor : Editor
{

	Editor settingsEditor;
	bool settingsFoldout;

	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();
		RDSimulation sim = target as RDSimulation;

		if (sim.settings != null)
		{
			DrawSettingsEditor(sim.settings, ref settingsFoldout, ref settingsEditor);
			EditorPrefs.SetBool(nameof(settingsFoldout), settingsFoldout);
		}
	}

	void DrawSettingsEditor(Object settings, ref bool foldout, ref Editor editor)
	{
		if (settings != null)
		{
			foldout = EditorGUILayout.InspectorTitlebar(foldout, settings);
			if (foldout)
			{
				CreateCachedEditor(settings, null, ref editor);
				editor.OnInspectorGUI();
			}

		}
	}

	private void OnEnable()
	{
		settingsFoldout = EditorPrefs.GetBool(nameof(settingsFoldout), false);
	}
}
