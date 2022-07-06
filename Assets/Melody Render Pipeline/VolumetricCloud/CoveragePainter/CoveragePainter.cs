using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.IO;

[ExecuteInEditMode]
public class CoveragePainter : MonoBehaviour
{
	[Header("Settings")]
    Material material;
	public VolumetricCloudSettings settings;
	[Header("Brush")]
	public Texture2D brushTexture;
	[Range(0, 256)]
	public float brushRadius;
	[Range(0, 1)]
	public float coverageOpacity;
	[Range(0, 1)]
	public float typeOpacity;
	public bool drawCoverage;
	public bool drawType;
	public bool blendValues;
	[Header("Position")]
	[HideInInspector]
	public Vector3 mousePosition;
	[HideInInspector]
	public Vector3 worldPosition;
	[HideInInspector]
	public Vector3 worldPosition_;
	[HideInInspector]
	public Vector2 coveragePosition;
	[Header("Textures")]
	public bool displayEnable;
	[SerializeField, HideInInspector]
	public RenderTexture coverageTexture;
	private Texture2D asset;
	private string path;



	void OnEnable()
	{
        material = new Material(Shader.Find("Hidden/Melody RP/VolumetricCloud/TextureBrush"));
        material.hideFlags = HideFlags.HideAndDontSave;
		asset = settings.coverageTexture;

		SceneView.duringSceneGui += this.OnScene;
    }

    void OnDisable()
	{
        DestroyImmediate(material);
        material = null;
        SceneView.duringSceneGui -= this.OnScene;
	}

	public void Render(Vector2 point, float radius, float coverageOpacity, float typeOpacity, bool drawCoverage, bool drawType, bool blendValues, RenderTexture target, Texture2D brushTexture = null)
	{
		point.x *= target.width;
		point.y *= target.height;
		RenderTexture previous = RenderTexture.active;
		RenderTexture buffer = RenderTexture.GetTemporary(target.width, target.height, target.depth, target.format, RenderTextureReadWrite.Linear);
		Graphics.Blit(target, buffer);
		RenderTexture.active = buffer;
		float tw = target.width;
		float th = target.height;
		float h = radius;
		float w = radius;
		float z = 0.0f;
		material.SetTexture("_MainTex", target);
		material.SetFloat("_CoverageOpacity", coverageOpacity);
		material.SetFloat("_TypeOpacity", typeOpacity);
		material.SetFloat("_ShouldDrawCoverage", drawCoverage ? 1.0f : 0.0f);
		material.SetFloat("_ShouldDrawType", drawType ? 1.0f : 0.0f);
		material.SetFloat("_ShouldBlendValues", blendValues ? 1.0f : 0.0f);
		if (brushTexture != null)
		{
			material.SetTexture("_BrushTexture", brushTexture);
		}
		material.SetFloat("_BrushTextureAlpha", brushTexture == null ? 0.0f : 1.0f);

		GL.PushMatrix();
		material.SetPass(0);
		//load an identiy(normalized) matrix into current model
		GL.LoadIdentity();
		//loads an orthographic projection into the projection matrix and loads an identity into the model and view matrices, left、right、bottom、top are current camera viewport
		GL.LoadPixelMatrix(0.0f, target.width, 0.0f, target.height);
		//draw a quad on the texture
		GL.Begin(GL.QUADS);

		//set uv1 coord with QUADS vertex position(so it is local UV)
		GL.MultiTexCoord2(0, 0.0f, 0.0f);
		//set uv2 coord with the proportion of the position of brush point in total texture size(so it is global UV)
		GL.MultiTexCoord2(1, (point.x - w) / tw, (point.y - h) / th);
		//set vertex position by the brush point
		GL.Vertex(new Vector3(point.x - w, point.y - h, z));

		GL.MultiTexCoord2(0, 1.0f, 0.0f);
		GL.MultiTexCoord2(1, (point.x + w) / tw, (point.y - h) / th);
		GL.Vertex(new Vector3(point.x + w, point.y - h, z));

		GL.MultiTexCoord2(0, 1.0f, 1.0f);
		GL.MultiTexCoord2(1, (point.x + w) / tw, (point.y + h) / th);
		GL.Vertex(new Vector3(point.x + w, point.y + h, z));

		GL.MultiTexCoord2(0, 0.0f, 1.0f);
		GL.MultiTexCoord2(1, (point.x - w) / tw, (point.y + h) / th);
		GL.Vertex(new Vector3(point.x - w, point.y + h, z));

		GL.End();
		GL.PopMatrix();

		Graphics.Blit(buffer, target);
		RenderTexture.ReleaseTemporary(buffer);
		RenderTexture.active = previous;
	}

	public void Clear(RenderTexture target)
	{
		RenderTexture previous = RenderTexture.active;
		RenderTexture.active = target;
		GL.Clear(true, true, Color.black);
		RenderTexture.active = previous;
	}

	void OnScene(SceneView scene)
	{
        if (Selection.Contains(gameObject))
        {
			Event e = Event.current;
			mousePosition = e.mousePosition;
			//get screen potion position
			float ppp = EditorGUIUtility.pixelsPerPoint;
			mousePosition.y = scene.camera.pixelHeight - mousePosition.y * ppp;
			mousePosition.x *= ppp;
			//screen to world
			worldPosition = ScreenPointToAtmosphere(mousePosition, scene.camera);
			worldPosition_ = worldPosition + new Vector3(0, settings.atmosphereEndHeight - settings.atmosphereStartHeight, 0);
			//world to coverage
			coveragePosition = WorldToCoverage(scene.camera.transform.position, worldPosition);

			float brushRadius = this.brushRadius / 10;
			bool drawCoverage = this.drawCoverage;
			bool drawType = this.drawType;
			bool blendValues = this.blendValues;
			float coverageOpacity = drawCoverage ? this.coverageOpacity : 0.0f;
			float typeOpacity = drawType ? this.typeOpacity : 0.0f;

			if (e.rawType == EventType.KeyDown)
            {
				if (e.keyCode == KeyCode.C)
                {
					Render(coveragePosition, brushRadius, coverageOpacity, typeOpacity, drawCoverage, drawType, blendValues, coverageTexture, brushTexture);
                }
				if (e.keyCode == KeyCode.V)
				{
					Render(coveragePosition, brushRadius, -coverageOpacity, -typeOpacity, drawCoverage, drawType, blendValues, coverageTexture, brushTexture);
				}
			}
		}
    }

	Vector3 ScreenPointToAtmosphere(Vector2 screenPoint, Camera camera)
	{
		Vector3 atmospherePoint = new Vector3();
		Ray ray =  camera.ScreenPointToRay(screenPoint);
		Vector3 origin = camera.transform.position;
		Vector3 rayDirection = ray.direction;
		float distance = RayIntersectCloudDistance(settings.earthCenter, origin, rayDirection).x;
		atmospherePoint = origin + distance * rayDirection;
		return atmospherePoint;
	}

	Vector2 RaySphereDst(Vector3 sphereCenter, float sphereRadius, Vector3 origin, Vector3 direction)
	{
		Vector3 oc = origin - sphereCenter;
		float b = Vector3.Dot(direction, oc);
		float c = Vector3.Dot(oc, oc) - sphereRadius * sphereRadius;
		float t = b * b - c;
		// CASE 1: ray intersects sphere(t > 0)
		// dstA is dst to nearest intersection, dstB dst to far intersection
		// CASE 2: ray touches sphere(t = 0)
		// dstA is the dst to intersection behind the ray, dstB is dst to forward intersection
		// CASE 3: ray misses sphere (t < 0)
		float delta = (float)Mathf.Sqrt(Mathf.Max(t, 0));
		float dstToSphere = Mathf.Max(-b - delta, 0);
		float dstInSphere = Mathf.Max(-b + delta - dstToSphere, 0);
		return new Vector2(dstToSphere, dstInSphere);
	}

	Vector2 RayIntersectCloudDistance(Vector3 sphereCenter, Vector3 origin, Vector3 direction)
	{
		Vector2 cloudDstMin = RaySphereDst(sphereCenter, settings.atmosphereStartHeight + settings.earthRadius, origin, direction);
		Vector2 cloudDstMax = RaySphereDst(sphereCenter, settings.atmosphereEndHeight + settings.earthRadius, origin, direction);
		float dstToCloud = 0;
		float dstInCloud = 0;
		float d = Vector3.Distance(origin, sphereCenter);
		//on the ground
		if (d <= settings.atmosphereStartHeight + settings.earthRadius)
		{
			Vector3 startPos = origin + direction * cloudDstMin.y;
			if (startPos.y >= 0)
			{
				dstToCloud = cloudDstMin.y;
				dstInCloud = cloudDstMax.y - cloudDstMin.y;
			}
			return new Vector2(dstToCloud, dstInCloud);
		}
		//in the cloud
		else if (d > settings.atmosphereStartHeight + settings.earthRadius && d <= settings.atmosphereEndHeight + settings.earthRadius)
		{
			dstToCloud = 0;
			dstInCloud = cloudDstMin.y > 0 ? cloudDstMin.x : cloudDstMax.y;
			return new Vector2(dstToCloud, dstInCloud);
		}
		//outside the cloud
		else
		{
			dstToCloud = cloudDstMax.x;
			dstInCloud = cloudDstMin.y > 0 ? cloudDstMin.x - dstToCloud : cloudDstMax.y;
		}
		return new Vector2(dstToCloud, dstInCloud);
	}

	float CalculateHorizontalDistance(float innerRadius, float outerRadius)
	{
		return Mathf.Sqrt((outerRadius * outerRadius) - (innerRadius * innerRadius));
	}

	float CalculateMaxDistance(Vector3 cameraPosition)
	{
		float maxDistance = CalculateHorizontalDistance(settings.earthRadius, settings.earthRadius + settings.atmosphereEndHeight);
		float h = cameraPosition.y - settings.earthRadius;
		float horizon = Mathf.Sqrt(2.0f * settings.earthRadius * h + h * h);
		return maxDistance;
	}

	Vector2 WorldToCoverage(Vector3 camera, Vector3 world, bool includeOffset = true)
	{
		world /= CalculateMaxDistance(camera);
		world.x = world.x * 0.5f + 0.5f;
		world.z = world.z * 0.5f + 0.5f;
		if (includeOffset)
		{
			world.x += settings.coverageOffsetX;
			world.z += settings.coverageOffsetY;
			world.x -= Mathf.Floor(world.x);
			world.z -= Mathf.Floor(world.z);
		}
		return new Vector2(world.x, world.z);
	}

	public void CreateCoverageRenderTexture()
    {
		DestoryCoverageRenderTexture();
		coverageTexture = new RenderTexture(512, 512, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
		coverageTexture.hideFlags = HideFlags.HideAndDontSave;
		coverageTexture.wrapMode = TextureWrapMode.Repeat;
		coverageTexture.useMipMap = true;
		RenderTexture previousRT = RenderTexture.active;
		Graphics.Blit(Texture2D.blackTexture, coverageTexture);
		RenderTexture.active = previousRT;
	}

	public void DestoryCoverageRenderTexture()
    {
		if(coverageTexture != null)
        {
			DestroyImmediate(coverageTexture);
			coverageTexture = null;
        }
    }

	public void CopyCoverageAssetToRenderTexture()
    {
		string path = AssetDatabase.GetAssetPath(asset);
		if (path == null)
		{
			throw new ArgumentException("Tried copying coverage to temp but asset doesn't exist");
		}
		RenderTexture previousRT = RenderTexture.active;
		Graphics.Blit(asset, coverageTexture);
		RenderTexture.active = previousRT;
		this.path = path;
	}

	public void SaveCoverageRenderTexture()
    {
		if (path != null)
		{
			SaveTempCoverageTo(path);
		}
	}

	void SaveTempCoverageTo(string path)
	{
		if (coverageTexture == null)
		{
			throw new NullReferenceException("Attempted to save null temp coverage");
		}
		RenderTexture oldActive = RenderTexture.active;
		Texture2D exported = new Texture2D(coverageTexture.width, coverageTexture.height, TextureFormat.ARGB32, false);
		RenderTexture.active = coverageTexture;
		exported.ReadPixels(new Rect(0.0f, 0.0f, coverageTexture.width, coverageTexture.height), 0, 0);
		RenderTexture.active = oldActive;
		byte[] bytes = exported.EncodeToPNG();
		DestroyImmediate(exported);
		if (bytes != null)
		{
			File.WriteAllBytes(path, bytes);
			AssetDatabase.Refresh();
			TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
			importer.textureType = TextureImporterType.Default;
			importer.sRGBTexture = true;
			importer.textureCompression = TextureImporterCompression.Compressed;
			importer.SaveAndReimport();
			AssetDatabase.Refresh();
		}
	}
}
