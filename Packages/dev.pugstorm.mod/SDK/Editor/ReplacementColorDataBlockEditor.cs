//using UnityEngine;
//using UnityEditor;
//using System.Collections.Generic;

//#if UNITY_EDITOR
//[CustomEditor(typeof(ReplacementColorDataBlock))]
//public class ReplacementColorDataBlockEditor : Editor
//{
//	private Texture2D cachedPreview;
//	private int lastColorHash;

//	private void OnDisable()
//	{
//		if (cachedPreview != null)
//		{
//			DestroyImmediate(cachedPreview);
//			cachedPreview = null;
//		}
//	}

//	public override void OnInspectorGUI()
//	{
//		DrawDefaultInspector();

//		var replacementColors = (ReplacementColorDataBlock)target;
//		var sourceColors = replacementColors.sourceColors.Get();

//		if (replacementColors.targetTexture == null || sourceColors == null || replacementColors.colors.Count != sourceColors.colors.Count)
//		{
//			return;
//		}

//		int currentColorHash = GetColorHash(replacementColors.colors, sourceColors);

//		if (cachedPreview == null || lastColorHash != currentColorHash)
//		{
//			if (cachedPreview != null)
//			{
//				DestroyImmediate(cachedPreview);
//			}

//			cachedPreview = GeneratePreview(replacementColors, sourceColors);
//			lastColorHash = currentColorHash;
//		}

//		if (cachedPreview != null)
//		{
//			float displayWidth = replacementColors.targetTexture.width * 3;
//			float displayHeight = replacementColors.targetTexture.height * 3;

//			Rect previewRect = GUILayoutUtility.GetRect(displayWidth, displayHeight);
//			GUI.DrawTexture(previewRect, cachedPreview, ScaleMode.ScaleToFit, true);
//		}
//	}

//	private Texture2D GeneratePreview(ReplacementColorDataBlock replacementColors, SourceColorDataBlock sourceColors)
//	{
//		var resultTexture = new Texture2D(replacementColors.targetTexture.width, replacementColors.targetTexture.height, TextureFormat.RGBA32, false)
//		{
//			filterMode = FilterMode.Point
//		};

//		var srcPixels = replacementColors.targetTexture.GetPixels();
//		var repPixels = new Color[srcPixels.Length];

//		var srcColors = sourceColors.colors;
//		var repColors = replacementColors.colors;

//		for (int i = 0; i < srcPixels.Length; i++)
//		{
//			var pixel = srcPixels[i];
//			bool replaced = false;

//			for (int j = 0; j < srcColors.Count; j++)
//			{
//				if ((Color32)pixel == srcColors[j])//we do this to prevent it from defaulting to Color since it uses float rounding and will result in different RGB values despite same hexcode
//				{
//					repPixels[i] = repColors[j];
//					replaced = true;
//					break;
//				}
//			}

//			if (!replaced)
//			{
//				repPixels[i] = pixel;
//			}
//		}

//		resultTexture.SetPixels(repPixels);
//		resultTexture.Apply();

//		return resultTexture;
//	}

//	private int GetColorHash(List<Color> colors, SourceColorDataBlock sourceColors)
//	{
//		int colorHash = colors.Count;
//		foreach (var color in colors)
//		{
//			colorHash = unchecked(colorHash * 31 + color.GetHashCode());
//		}
//		colorHash = unchecked(colorHash * 31 + (sourceColors != null ? sourceColors.GetInstanceID() : 0));
//		return colorHash;
//	}
//}
//#endif

