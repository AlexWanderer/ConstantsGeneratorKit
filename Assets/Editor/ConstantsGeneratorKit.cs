using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;
using System.Linq;
using System.Reflection;


namespace Prime31.Editor
{
	// Note: This class uses UnityEditorInternal which is an undocumented internal feature
	public class ConstantsGeneratorKit : MonoBehaviour
	{
		private const string FOLDER_LOCATION = "scripts/auto-generated/";
		private const string NAMESPACE = "k";
		private static string[] IGNORE_RESOURCES_IN_SUBFOLDERS = new string[] { "ProCore", "2DToolkit" };

		private const string TAGS_FILE_NAME = "Tags.cs";
		private const string LAYERS_FILE_NAME = "Layers.cs";
		private const string SORTING_LAYERS_FILE_NAME = "SortingLayers.cs";
		private const string SCENES_FILE_NAME = "Scenes.cs";
		private const string RESOURCE_PATHS_FILE_NAME = "Resources.cs";


		[MenuItem( "Edit/Generate Constants Classes..." )]
		static void rebuildConstantsClassesMenuItem()
		{
			rebuildConstantsClasses();
		}


		public static void rebuildConstantsClasses( bool buildResources = true, bool buildScenes = true, bool buildTagsAndLayers = true, bool buildSortingLayers = true )
		{
			var folderPath = Application.dataPath + "/" + FOLDER_LOCATION;
			if( !Directory.Exists(folderPath ) )
				Directory.CreateDirectory( folderPath );

			if( buildTagsAndLayers )
			{
				File.WriteAllText( folderPath + TAGS_FILE_NAME, getClassContent( TAGS_FILE_NAME.Replace( ".cs", string.Empty ), UnityEditorInternal.InternalEditorUtility.tags ) );
				File.WriteAllText( folderPath + LAYERS_FILE_NAME, getLayerClassContent( LAYERS_FILE_NAME.Replace( ".cs", string.Empty ), UnityEditorInternal.InternalEditorUtility.layers ) );

				AssetDatabase.ImportAsset( "Assets/" + FOLDER_LOCATION + TAGS_FILE_NAME, ImportAssetOptions.ForceUpdate );
				AssetDatabase.ImportAsset( "Assets/" + FOLDER_LOCATION + LAYERS_FILE_NAME, ImportAssetOptions.ForceUpdate );
			}

			/* removing this until I have time to figure out where the sorting layers are in Unity 5
			if( buildSortingLayers )
			{
				var sortingLayers = getSortingLayers();
				var layerIds = getSortingLayerIds( sortingLayers.Length );
				File.WriteAllText( folderPath + SORTING_LAYERS_FILE_NAME, getSortingLayerClassContent( SORTING_LAYERS_FILE_NAME.Replace( ".cs", string.Empty ), sortingLayers, layerIds ) );
			}
			*/

			// handle resources and scenes only when asked
			if( buildScenes )
			{
				File.WriteAllText( folderPath + SCENES_FILE_NAME, getClassContent( SCENES_FILE_NAME.Replace( ".cs", string.Empty ), editorBuildSettingsScenesToNameStrings( EditorBuildSettings.scenes ) ) );
				AssetDatabase.ImportAsset( "Assets/" + FOLDER_LOCATION + SCENES_FILE_NAME, ImportAssetOptions.ForceUpdate );
			}

			if( buildResources )
			{
				File.WriteAllText( folderPath + RESOURCE_PATHS_FILE_NAME, getResourcePathsContent( RESOURCE_PATHS_FILE_NAME.Replace( ".cs", string.Empty ) ) );
				AssetDatabase.ImportAsset( "Assets/" + FOLDER_LOCATION + RESOURCE_PATHS_FILE_NAME, ImportAssetOptions.ForceUpdate );
			}

			if( buildResources && buildScenes && buildTagsAndLayers )
				Debug.Log( "ConstantsGeneratorKit complete. Constants classes built to " + FOLDER_LOCATION );
		}


		private static string[] editorBuildSettingsScenesToNameStrings( EditorBuildSettingsScene[] scenes )
		{
			var sceneNames = new string[scenes.Length];
			for( var n = 0; n < sceneNames.Length; n++ )
				sceneNames[n] = System.IO.Path.GetFileNameWithoutExtension( scenes[n].path );

			return sceneNames;
		}


		private static string[] getSortingLayers()
		{
			var type = typeof( UnityEditorInternal.InternalEditorUtility );
			var prop = type.GetProperty( "sortingLayerNames", BindingFlags.Static | BindingFlags.NonPublic );

			return prop.GetValue( null, null ) as string[];
		}


		private static int[] getSortingLayerIds( int totalSortingLayers )
		{
			var type = typeof( UnityEditorInternal.InternalEditorUtility );

			// this appears to be missing from Unity 5...
			var method = type.GetMethod( "GetSortingLayerUserID", BindingFlags.Static | BindingFlags.NonPublic );

			var layerIds = new int[totalSortingLayers];
			for( var n = 0; n < totalSortingLayers; n++ )
				layerIds[n] = (int)method.Invoke( null, new object[] { n } );

			return layerIds;
		}


		private static string getClassContent( string className, string[] labelsArray )
		{
			var output = "";
			output += "//This class is auto-generated do not modify\n";
			output += "namespace " + NAMESPACE + "\n";
			output += "{\n";
			output += "\tpublic static class " + className + "\n";
			output += "\t{\n";

			foreach( var label in labelsArray )
				output += "\t\t" + buildConstVariable( label ) + "\n";

			if( className == SCENES_FILE_NAME.Replace( ".cs", string.Empty ) )
			{
				output += "\n\t\tpublic const int TOTAL_SCENES = " + labelsArray.Length + ";\n\n\n";

				output += "\t\tpublic static int nextSceneIndex()\n";
				output += "\t\t{\n";
				output += "\t\t\tif( UnityEngine.Application.loadedLevel + 1 == TOTAL_SCENES )\n";
				output += "\t\t\t\treturn 0;\n";
				output += "\t\t\treturn UnityEngine.Application.loadedLevel + 1;\n";
				output += "\t\t}\n";
			}

			output += "\t}\n";
			output += "}";

			return output;
		}


		private class Resource
		{
			public string name;
			public string path;


			public Resource( string path )
			{
				// get the path from the Resources folder root
				var parts = path.Split( new string[] { "Resources/" }, System.StringSplitOptions.RemoveEmptyEntries );

				// strip the extension from the path
				this.path = parts[1].Replace( Path.GetFileName( parts[1] ), Path.GetFileNameWithoutExtension( parts[1] ) );
				this.name = Path.GetFileNameWithoutExtension( parts[1] );
			}
		}


		private static string getResourcePathsContent( string className )
		{
			var output = "";
			output += "//This class is auto-generated do not modify\n";
			output += "namespace " + NAMESPACE + "\n";
			output += "{\n";
			output += "\tpublic static class " + className + "\n";
			output += "\t{\n";


			// find all our Resources folders
			var dirs = Directory.GetDirectories( Application.dataPath, "Resources", SearchOption.AllDirectories );
			var resources = new List<Resource>();

			foreach( var dir in dirs )
			{
				// limit our ignored folders
				var shouldAddFolder = true;
				foreach( var ignoredDir in IGNORE_RESOURCES_IN_SUBFOLDERS )
				{
					if( dir.Contains( ignoredDir ) )
					{
						Debug.LogWarning( "DONT ADD FOLDER + " + dir );
						shouldAddFolder = false;
						continue;
					}
				}

				if( shouldAddFolder )
					resources.AddRange( getAllResourcesAtPath( dir ) );
			}

			var resourceNamesAdded = new List<string>();
			foreach( var res in resources )
			{
				if( resourceNamesAdded.Contains( res.name ) )
				{
					Debug.LogWarning( "multiple resources with name " + res.name + " found. Skipping " + res.path );
					continue;
				}

				output += "\t\t" + buildConstVariable( res.name, "", res.path ) + "\n";
				resourceNamesAdded.Add( res.name );
			}


			output += "\t}\n";
			output += "}";

			return output;
		}


		private static List<Resource> getAllResourcesAtPath( string path )
		{
			var resources = new List<Resource>();

			// handle files
			var files = Directory.GetFiles( path, "*", SearchOption.AllDirectories );
			foreach( var f in files )
			{
				if( f.EndsWith( ".meta" ) || f.EndsWith( ".db" ) || f.EndsWith( ".DS_Store" ) )
					continue;

				resources.Add( new Resource( f ) );
			}

			return resources;
		}


		private static string getLayerClassContent( string className, string[] labelsArray )
		{
			var output = "";
			output += "// This class is auto-generated do not modify\n";
			output += "namespace " + NAMESPACE + "\n";
			output += "{\n";
			output += "\tpublic static class " + className + "\n";
			output += "\t{\n";

			foreach( var label in labelsArray )
				output += "\t\t" + "public const int " + toUpperCaseWithUnderscores( label ) + " = " + LayerMask.NameToLayer( label ) + ";\n";

			output += "\n\n";
			output += @"		public static int onlyIncluding( params int[] layers )
		{
			int mask = 0;
			for( var i = 0; i < layers.Length; i++ )
				mask |= ( 1 << layers[i] );

			return mask;
		}


		public static int everythingBut( params int[] layers )
		{
			return ~onlyIncluding( layers );
		}";

			output += "\n";
			output += "\t}\n";
			output += "}";

			return output;
		}


		private static string getSortingLayerClassContent( string className, string[] sortingLayers, int[] layerIds )
		{
			var output = "";
			output += "// This class is auto-generated do not modify\n";
			output += "namespace " + NAMESPACE + "\n";
			output += "{\n";
			output += "\tpublic static class " + className + "\n";
			output += "\t{\n";

			for( var i = 0; i < sortingLayers.Length; i++ )
				output += "\t\t" + "public const int " + toUpperCaseWithUnderscores( sortingLayers[i] ) + " = " + layerIds[i] + ";\n";

			output += "\n";
			output += "\t}\n";
			output += "}";

			return output;
		}


		private static string buildConstVariable( string varName, string suffix = "", string value = null )
		{
			value = value ?? varName;
			return "public const string " + toUpperCaseWithUnderscores( varName ) + suffix + " = " + '"' + value + '"' + ";";
		}


		private static string toUpperCaseWithUnderscores( string input )
		{
			input = input.Replace( "-", "_" ).Replace( " ", "_" );

			// make camel-case have an underscore between letters
			Func<char,int,string> func = ( x, i ) =>
			{
				if( i > 0 && char.IsUpper( x ) && char.IsLower( input[i - 1] ) )
				return "_" + x.ToString();
				return x.ToString();
			};
			input = string.Concat( input.Select( func ).ToArray() );

			// digits are a no-no so stick a "k" in front
			if( Char.IsDigit( input[0] ) )
				return "k" + input.ToUpper();
			return input.ToUpper();
		}
	}


	// this post processor listens for changes to the TagManager and automatically rebuilds all classes if it sees a change
	public class ConstandsGeneratorPostProcessor : AssetPostprocessor
	{
		// for some reason, OnPostprocessAllAssets often gets called multiple times in a row. This helps guard against rebuilding classes
		// when not necessary.
		static DateTime? _lastTagsAndLayersBuildTime;
		static DateTime? _lastScenesBuildTime;


		static void OnPostprocessAllAssets( string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths )
		{
			var resourcesDidChange = importedAssets.Any( s => Regex.IsMatch( s, @"/Resources/.*", System.Text.RegularExpressions.RegexOptions.IgnoreCase ) );

			if( !resourcesDidChange )
				resourcesDidChange = movedAssets.Any( s => Regex.IsMatch( s, @"/Resources/.*", System.Text.RegularExpressions.RegexOptions.IgnoreCase ) );

			if( !resourcesDidChange )
				resourcesDidChange = deletedAssets.Any( s => Regex.IsMatch( s, @"/Resources/.*", System.Text.RegularExpressions.RegexOptions.IgnoreCase ) );

			if( resourcesDidChange )
				ConstantsGeneratorKit.rebuildConstantsClasses( true, false, false );


			// layers and tags changes
			if( importedAssets.Contains( "ProjectSettings/TagManager.asset" ) )
			{
				if( !_lastTagsAndLayersBuildTime.HasValue || _lastTagsAndLayersBuildTime.Value.AddSeconds( 5 ) < DateTime.Now )
				{
					_lastTagsAndLayersBuildTime = DateTime.Now;
					ConstantsGeneratorKit.rebuildConstantsClasses( false, false );
				}
			}


			// scene changes
			if( importedAssets.Contains( "ProjectSettings/EditorBuildSettings.asset" ) )
			{
				if( !_lastScenesBuildTime.HasValue || _lastScenesBuildTime.Value.AddSeconds( 5 ) < DateTime.Now )
				{
					_lastScenesBuildTime = DateTime.Now;
					ConstantsGeneratorKit.rebuildConstantsClasses( false, true );
				}
			}
		}
	}
}