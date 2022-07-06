using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(CASimulation))]
public class CASimulationEditor : Editor
{

	Editor settingsEditor;
	bool settingsFoldout;

	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();
		CASimulation sim = target as CASimulation;

		if (GUILayout.Button("Restart"))
		{
			sim.Reset();
		}

		if (GUILayout.Button("Randomize Conditions"))
		{

			int seed = new System.Random().Next();

			Undo.RecordObject(sim.settings, "Randomize Conditions");
			sim.settings.RandomizeConditions(seed);

			/// Extremely gross workaround for a weird bug:
			// After leaving playmode, the inspector values on the settings object don't refresh anymore
			// when changed from script, unless I perform an undo (after which it works perfectly).
			// So, just undo and then redo the changes here.
			Undo.PerformUndo();
			Undo.PerformRedo();

			if (Application.isPlaying)
			{
				sim.Reset();
			}
		}

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
