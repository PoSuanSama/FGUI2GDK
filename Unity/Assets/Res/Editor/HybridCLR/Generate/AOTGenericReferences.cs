using System.Collections.Generic;
public class AOTGenericReferences : UnityEngine.MonoBehaviour
{

	// {{ AOT assemblies
	public static readonly IReadOnlyList<string> PatchedAOTAssemblyList = new List<string>
	{
		"Game.Hot.Loader.dll",
		"GameFramework.dll",
		"LubanLib.dll",
		"System.dll",
		"UniTask.dll",
		"UnityEngine.CoreModule.dll",
		"UnityGameFramework.Extension.dll",
		"UnityGameFramework.Runtime.dll",
		"mscorlib.dll",
	};
	// }}

	// {{ constraint implement type
	// }} 

	// {{ AOT generic types
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Game.Hot.DTAircraft.<LoadAsync>d__4>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Game.Hot.DTArmor.<LoadAsync>d__4>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Game.Hot.DTAsteroid.<LoadAsync>d__4>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Game.Hot.DTInventory.<LoadAsync>d__4>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Game.Hot.DTOneConfig.<LoadAsync>d__5>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Game.Hot.DTThruster.<LoadAsync>d__4>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Game.Hot.DTWeapon.<LoadAsync>d__4>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Game.Hot.FairyDialogFlow.<OpenAsync>d__0,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Game.Hot.FairyInventoryFlow.<OpenDetailAsync>d__12>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Game.Hot.FairyInventoryFlow.<OpenInventoryAsync>d__11>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Game.Hot.FairyInventoryFlow.<OpenOverlayAsync>d__13>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Game.Hot.TablesComponent.<<LoadAllAsync>g__LoadByteBuf|37_0>d,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Game.Hot.TablesComponent.<<LoadAllAsync>g__LoadJson|37_1>d,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Game.Hot.TablesComponent.<LoadAllAsync>d__37>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Game.Hot.TablesComponent.<LoadAsync>d__32>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Game.Hot.DTAircraft.<LoadAsync>d__4>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Game.Hot.DTArmor.<LoadAsync>d__4>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Game.Hot.DTAsteroid.<LoadAsync>d__4>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Game.Hot.DTInventory.<LoadAsync>d__4>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Game.Hot.DTOneConfig.<LoadAsync>d__5>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Game.Hot.DTThruster.<LoadAsync>d__4>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Game.Hot.DTWeapon.<LoadAsync>d__4>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Game.Hot.FairyDialogFlow.<OpenAsync>d__0,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Game.Hot.FairyInventoryFlow.<OpenDetailAsync>d__12>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Game.Hot.FairyInventoryFlow.<OpenInventoryAsync>d__11>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Game.Hot.FairyInventoryFlow.<OpenOverlayAsync>d__13>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Game.Hot.TablesComponent.<<LoadAllAsync>g__LoadByteBuf|37_0>d,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Game.Hot.TablesComponent.<<LoadAllAsync>g__LoadJson|37_1>d,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Game.Hot.TablesComponent.<LoadAllAsync>d__37>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Game.Hot.TablesComponent.<LoadAsync>d__32>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<Game.Hot.FairyDemoForm.<OpenInventoryAsync>d__41>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<Game.Hot.FairyInventoryForm.<OpenDetailAsync>d__37>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<Game.Hot.FairyInventoryForm.<OpenOverlayAsync>d__38>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<Game.Hot.FairyItemDetailForm.<OpenOverlayAsync>d__42>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<Game.Hot.ProcedureMenu.<OpenFairyDemoAsync>d__7>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<Game.Hot.ProcedurePreload.<PreloadAsync>d__1>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<Game.Hot.FairyDemoForm.<OpenInventoryAsync>d__41>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<Game.Hot.FairyInventoryForm.<OpenDetailAsync>d__37>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<Game.Hot.FairyInventoryForm.<OpenOverlayAsync>d__38>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<Game.Hot.FairyItemDetailForm.<OpenOverlayAsync>d__42>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<Game.Hot.ProcedureMenu.<OpenFairyDemoAsync>d__7>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<Game.Hot.ProcedurePreload.<PreloadAsync>d__1>
	// Cysharp.Threading.Tasks.CompilerServices.IStateMachineRunnerPromise<object>
	// Cysharp.Threading.Tasks.ITaskPoolNode<object>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,object>>
	// Cysharp.Threading.Tasks.IUniTaskSource<object>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,object>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<object>
	// Cysharp.Threading.Tasks.UniTask.CanceledResultSource<object>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,object>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<object>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,object>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<object>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,object>>
	// Cysharp.Threading.Tasks.UniTask<object>
	// Cysharp.Threading.Tasks.UniTaskCompletionSourceCore<Cysharp.Threading.Tasks.AsyncUnit>
	// Cysharp.Threading.Tasks.UniTaskCompletionSourceCore<object>
	// GameFramework.GameFrameworkAction<object>
	// GameFramework.GameFrameworkLinkedList.Enumerator<object>
	// GameFramework.GameFrameworkLinkedList<object>
	// GameFramework.Variable<byte>
	// System.Action<Cysharp.Threading.Tasks.UniTask>
	// System.Action<byte>
	// System.Action<int>
	// System.Action<object,object>
	// System.Action<object>
	// System.Collections.Generic.ArraySortHelper<Cysharp.Threading.Tasks.UniTask>
	// System.Collections.Generic.ArraySortHelper<byte>
	// System.Collections.Generic.ArraySortHelper<int>
	// System.Collections.Generic.ArraySortHelper<object>
	// System.Collections.Generic.Comparer<Cysharp.Threading.Tasks.UniTask>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,object>>
	// System.Collections.Generic.Comparer<byte>
	// System.Collections.Generic.Comparer<int>
	// System.Collections.Generic.Comparer<object>
	// System.Collections.Generic.ComparisonComparer<Cysharp.Threading.Tasks.UniTask>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,object>>
	// System.Collections.Generic.ComparisonComparer<byte>
	// System.Collections.Generic.ComparisonComparer<int>
	// System.Collections.Generic.ComparisonComparer<object>
	// System.Collections.Generic.Dictionary.Enumerator<Game.Hot.AIUtility.CampPair,byte>
	// System.Collections.Generic.Dictionary.Enumerator<System.Collections.Generic.KeyValuePair<byte,byte>,object>
	// System.Collections.Generic.Dictionary.Enumerator<byte,object>
	// System.Collections.Generic.Dictionary.Enumerator<int,long>
	// System.Collections.Generic.Dictionary.Enumerator<int,object>
	// System.Collections.Generic.Dictionary.Enumerator<object,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<Game.Hot.AIUtility.CampPair,byte>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<System.Collections.Generic.KeyValuePair<byte,byte>,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<byte,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<int,long>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<int,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<object,object>
	// System.Collections.Generic.Dictionary.KeyCollection<Game.Hot.AIUtility.CampPair,byte>
	// System.Collections.Generic.Dictionary.KeyCollection<System.Collections.Generic.KeyValuePair<byte,byte>,object>
	// System.Collections.Generic.Dictionary.KeyCollection<byte,object>
	// System.Collections.Generic.Dictionary.KeyCollection<int,long>
	// System.Collections.Generic.Dictionary.KeyCollection<int,object>
	// System.Collections.Generic.Dictionary.KeyCollection<object,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<Game.Hot.AIUtility.CampPair,byte>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<System.Collections.Generic.KeyValuePair<byte,byte>,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<byte,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<int,long>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<int,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<object,object>
	// System.Collections.Generic.Dictionary.ValueCollection<Game.Hot.AIUtility.CampPair,byte>
	// System.Collections.Generic.Dictionary.ValueCollection<System.Collections.Generic.KeyValuePair<byte,byte>,object>
	// System.Collections.Generic.Dictionary.ValueCollection<byte,object>
	// System.Collections.Generic.Dictionary.ValueCollection<int,long>
	// System.Collections.Generic.Dictionary.ValueCollection<int,object>
	// System.Collections.Generic.Dictionary.ValueCollection<object,object>
	// System.Collections.Generic.Dictionary<Game.Hot.AIUtility.CampPair,byte>
	// System.Collections.Generic.Dictionary<System.Collections.Generic.KeyValuePair<byte,byte>,object>
	// System.Collections.Generic.Dictionary<byte,object>
	// System.Collections.Generic.Dictionary<int,long>
	// System.Collections.Generic.Dictionary<int,object>
	// System.Collections.Generic.Dictionary<object,object>
	// System.Collections.Generic.EqualityComparer<Game.Hot.AIUtility.CampPair>
	// System.Collections.Generic.EqualityComparer<System.Collections.Generic.KeyValuePair<byte,byte>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,object>>
	// System.Collections.Generic.EqualityComparer<byte>
	// System.Collections.Generic.EqualityComparer<int>
	// System.Collections.Generic.EqualityComparer<long>
	// System.Collections.Generic.EqualityComparer<object>
	// System.Collections.Generic.ICollection<Cysharp.Threading.Tasks.UniTask>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<Game.Hot.AIUtility.CampPair,byte>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<System.Collections.Generic.KeyValuePair<byte,byte>,object>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<byte,object>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<int,long>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.ICollection<byte>
	// System.Collections.Generic.ICollection<int>
	// System.Collections.Generic.ICollection<object>
	// System.Collections.Generic.IComparer<Cysharp.Threading.Tasks.UniTask>
	// System.Collections.Generic.IComparer<byte>
	// System.Collections.Generic.IComparer<int>
	// System.Collections.Generic.IComparer<object>
	// System.Collections.Generic.IEnumerable<Cysharp.Threading.Tasks.UniTask>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<Game.Hot.AIUtility.CampPair,byte>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<System.Collections.Generic.KeyValuePair<byte,byte>,object>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<byte,object>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,long>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.IEnumerable<byte>
	// System.Collections.Generic.IEnumerable<int>
	// System.Collections.Generic.IEnumerable<object>
	// System.Collections.Generic.IEnumerator<Cysharp.Threading.Tasks.UniTask>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<Game.Hot.AIUtility.CampPair,byte>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<System.Collections.Generic.KeyValuePair<byte,byte>,object>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<byte,object>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<int,long>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.IEnumerator<byte>
	// System.Collections.Generic.IEnumerator<int>
	// System.Collections.Generic.IEnumerator<object>
	// System.Collections.Generic.IEqualityComparer<Game.Hot.AIUtility.CampPair>
	// System.Collections.Generic.IEqualityComparer<System.Collections.Generic.KeyValuePair<byte,byte>>
	// System.Collections.Generic.IEqualityComparer<byte>
	// System.Collections.Generic.IEqualityComparer<int>
	// System.Collections.Generic.IEqualityComparer<object>
	// System.Collections.Generic.IList<Cysharp.Threading.Tasks.UniTask>
	// System.Collections.Generic.IList<byte>
	// System.Collections.Generic.IList<int>
	// System.Collections.Generic.IList<object>
	// System.Collections.Generic.IReadOnlyCollection<object>
	// System.Collections.Generic.IReadOnlyDictionary<int,object>
	// System.Collections.Generic.KeyValuePair<Game.Hot.AIUtility.CampPair,byte>
	// System.Collections.Generic.KeyValuePair<System.Collections.Generic.KeyValuePair<byte,byte>,object>
	// System.Collections.Generic.KeyValuePair<byte,byte>
	// System.Collections.Generic.KeyValuePair<byte,object>
	// System.Collections.Generic.KeyValuePair<int,long>
	// System.Collections.Generic.KeyValuePair<int,object>
	// System.Collections.Generic.KeyValuePair<object,object>
	// System.Collections.Generic.LinkedList.Enumerator<object>
	// System.Collections.Generic.LinkedList<object>
	// System.Collections.Generic.LinkedListNode<object>
	// System.Collections.Generic.List.Enumerator<Cysharp.Threading.Tasks.UniTask>
	// System.Collections.Generic.List.Enumerator<byte>
	// System.Collections.Generic.List.Enumerator<int>
	// System.Collections.Generic.List.Enumerator<object>
	// System.Collections.Generic.List<Cysharp.Threading.Tasks.UniTask>
	// System.Collections.Generic.List<byte>
	// System.Collections.Generic.List<int>
	// System.Collections.Generic.List<object>
	// System.Collections.Generic.ObjectComparer<Cysharp.Threading.Tasks.UniTask>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,object>>
	// System.Collections.Generic.ObjectComparer<byte>
	// System.Collections.Generic.ObjectComparer<int>
	// System.Collections.Generic.ObjectComparer<object>
	// System.Collections.Generic.ObjectEqualityComparer<Game.Hot.AIUtility.CampPair>
	// System.Collections.Generic.ObjectEqualityComparer<System.Collections.Generic.KeyValuePair<byte,byte>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,object>>
	// System.Collections.Generic.ObjectEqualityComparer<byte>
	// System.Collections.Generic.ObjectEqualityComparer<int>
	// System.Collections.Generic.ObjectEqualityComparer<long>
	// System.Collections.Generic.ObjectEqualityComparer<object>
	// System.Collections.Generic.Queue.Enumerator<object>
	// System.Collections.Generic.Queue<object>
	// System.Collections.ObjectModel.ReadOnlyCollection<Cysharp.Threading.Tasks.UniTask>
	// System.Collections.ObjectModel.ReadOnlyCollection<byte>
	// System.Collections.ObjectModel.ReadOnlyCollection<int>
	// System.Collections.ObjectModel.ReadOnlyCollection<object>
	// System.Comparison<Cysharp.Threading.Tasks.UniTask>
	// System.Comparison<byte>
	// System.Comparison<int>
	// System.Comparison<object>
	// System.EventHandler<object>
	// System.Func<Cysharp.Threading.Tasks.UniTask<object>>
	// System.Func<System.ValueTuple<byte,object>>
	// System.Func<int>
	// System.Func<object,Cysharp.Threading.Tasks.UniTask<object>>
	// System.Func<object,System.ValueTuple<byte,object>>
	// System.Func<object,byte,object>
	// System.Func<object,object,object>
	// System.Func<object,object>
	// System.Func<object>
	// System.Nullable<UnityEngine.Rect>
	// System.Predicate<Cysharp.Threading.Tasks.UniTask>
	// System.Predicate<byte>
	// System.Predicate<int>
	// System.Predicate<object>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,object>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<object>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<object>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,object>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<object>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,object>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<object>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,object>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<object>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,object>>
	// System.Runtime.CompilerServices.TaskAwaiter<object>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,object>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<object>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,object>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<object>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,object>>
	// System.Threading.Tasks.Sources.IValueTaskSource<object>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,object>>
	// System.Threading.Tasks.Task<object>
	// System.Threading.Tasks.TaskFactory.<>c<object>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass32_0<object>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<object>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,object>>
	// System.Threading.Tasks.TaskFactory<object>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,object>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<object>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,object>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<object>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,object>>
	// System.Threading.Tasks.ValueTask<object>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,object>>
	// System.ValueTuple<byte,object>
	// UnityGameFramework.Extension.Awaitable.<>c__DisplayClass22_0<object>
	// UnityGameFramework.Extension.Awaitable.MoveNextFunc<object>
	// UnityGameFramework.Extension.Awaitable.UniTaskConfiguredSource.<>c<object>
	// UnityGameFramework.Extension.Awaitable.UniTaskConfiguredSource<object>
	// UnityGameFramework.Extension.UGFList<object>
	// }}

	public void RefMethods()
	{
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Game.Hot.TablesComponent.<LoadAllAsync>d__37>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Game.Hot.TablesComponent.<LoadAllAsync>d__37&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Game.Hot.TablesComponent.<LoadAsync>d__32>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Game.Hot.TablesComponent.<LoadAsync>d__32&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,Game.Hot.DTAircraft.<LoadAsync>d__4>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,Game.Hot.DTAircraft.<LoadAsync>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,Game.Hot.DTArmor.<LoadAsync>d__4>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,Game.Hot.DTArmor.<LoadAsync>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,Game.Hot.DTAsteroid.<LoadAsync>d__4>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,Game.Hot.DTAsteroid.<LoadAsync>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,Game.Hot.DTInventory.<LoadAsync>d__4>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,Game.Hot.DTInventory.<LoadAsync>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,Game.Hot.DTOneConfig.<LoadAsync>d__5>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,Game.Hot.DTOneConfig.<LoadAsync>d__5&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,Game.Hot.DTThruster.<LoadAsync>d__4>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,Game.Hot.DTThruster.<LoadAsync>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,Game.Hot.DTWeapon.<LoadAsync>d__4>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,Game.Hot.DTWeapon.<LoadAsync>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,Game.Hot.FairyInventoryFlow.<OpenDetailAsync>d__12>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,Game.Hot.FairyInventoryFlow.<OpenDetailAsync>d__12&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,Game.Hot.FairyInventoryFlow.<OpenInventoryAsync>d__11>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,Game.Hot.FairyInventoryFlow.<OpenInventoryAsync>d__11&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,Game.Hot.FairyInventoryFlow.<OpenOverlayAsync>d__13>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,Game.Hot.FairyInventoryFlow.<OpenOverlayAsync>d__13&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,Game.Hot.FairyDialogFlow.<OpenAsync>d__0>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,Game.Hot.FairyDialogFlow.<OpenAsync>d__0&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,Game.Hot.TablesComponent.<<LoadAllAsync>g__LoadByteBuf|37_0>d>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,Game.Hot.TablesComponent.<<LoadAllAsync>g__LoadByteBuf|37_0>d&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,Game.Hot.TablesComponent.<<LoadAllAsync>g__LoadJson|37_1>d>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,Game.Hot.TablesComponent.<<LoadAllAsync>g__LoadJson|37_1>d&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Game.Hot.DTAircraft.<LoadAsync>d__4>(Game.Hot.DTAircraft.<LoadAsync>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Game.Hot.DTArmor.<LoadAsync>d__4>(Game.Hot.DTArmor.<LoadAsync>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Game.Hot.DTAsteroid.<LoadAsync>d__4>(Game.Hot.DTAsteroid.<LoadAsync>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Game.Hot.DTInventory.<LoadAsync>d__4>(Game.Hot.DTInventory.<LoadAsync>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Game.Hot.DTOneConfig.<LoadAsync>d__5>(Game.Hot.DTOneConfig.<LoadAsync>d__5&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Game.Hot.DTThruster.<LoadAsync>d__4>(Game.Hot.DTThruster.<LoadAsync>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Game.Hot.DTWeapon.<LoadAsync>d__4>(Game.Hot.DTWeapon.<LoadAsync>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Game.Hot.FairyInventoryFlow.<OpenDetailAsync>d__12>(Game.Hot.FairyInventoryFlow.<OpenDetailAsync>d__12&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Game.Hot.FairyInventoryFlow.<OpenInventoryAsync>d__11>(Game.Hot.FairyInventoryFlow.<OpenInventoryAsync>d__11&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Game.Hot.FairyInventoryFlow.<OpenOverlayAsync>d__13>(Game.Hot.FairyInventoryFlow.<OpenOverlayAsync>d__13&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Game.Hot.TablesComponent.<LoadAllAsync>d__37>(Game.Hot.TablesComponent.<LoadAllAsync>d__37&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Game.Hot.TablesComponent.<LoadAsync>d__32>(Game.Hot.TablesComponent.<LoadAsync>d__32&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<Game.Hot.FairyDialogFlow.<OpenAsync>d__0>(Game.Hot.FairyDialogFlow.<OpenAsync>d__0&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<Game.Hot.TablesComponent.<<LoadAllAsync>g__LoadByteBuf|37_0>d>(Game.Hot.TablesComponent.<<LoadAllAsync>g__LoadByteBuf|37_0>d&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<Game.Hot.TablesComponent.<<LoadAllAsync>g__LoadJson|37_1>d>(Game.Hot.TablesComponent.<<LoadAllAsync>g__LoadJson|37_1>d&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Game.Hot.FairyDemoForm.<OpenInventoryAsync>d__41>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Game.Hot.FairyDemoForm.<OpenInventoryAsync>d__41&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Game.Hot.FairyInventoryForm.<OpenDetailAsync>d__37>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Game.Hot.FairyInventoryForm.<OpenDetailAsync>d__37&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Game.Hot.FairyInventoryForm.<OpenOverlayAsync>d__38>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Game.Hot.FairyInventoryForm.<OpenOverlayAsync>d__38&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Game.Hot.FairyItemDetailForm.<OpenOverlayAsync>d__42>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Game.Hot.FairyItemDetailForm.<OpenOverlayAsync>d__42&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Game.Hot.ProcedurePreload.<PreloadAsync>d__1>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Game.Hot.ProcedurePreload.<PreloadAsync>d__1&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,Game.Hot.ProcedureMenu.<OpenFairyDemoAsync>d__7>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,Game.Hot.ProcedureMenu.<OpenFairyDemoAsync>d__7&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<Game.Hot.FairyDemoForm.<OpenInventoryAsync>d__41>(Game.Hot.FairyDemoForm.<OpenInventoryAsync>d__41&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<Game.Hot.FairyInventoryForm.<OpenDetailAsync>d__37>(Game.Hot.FairyInventoryForm.<OpenDetailAsync>d__37&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<Game.Hot.FairyInventoryForm.<OpenOverlayAsync>d__38>(Game.Hot.FairyInventoryForm.<OpenOverlayAsync>d__38&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<Game.Hot.FairyItemDetailForm.<OpenOverlayAsync>d__42>(Game.Hot.FairyItemDetailForm.<OpenOverlayAsync>d__42&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<Game.Hot.ProcedureMenu.<OpenFairyDemoAsync>d__7>(Game.Hot.ProcedureMenu.<OpenFairyDemoAsync>d__7&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<Game.Hot.ProcedurePreload.<PreloadAsync>d__1>(Game.Hot.ProcedurePreload.<PreloadAsync>d__1&)
		// Cysharp.Threading.Tasks.UniTask<object> Cysharp.Threading.Tasks.UniTask.FromCanceled<object>(System.Threading.CancellationToken)
		// object Game.Hot.HotComponentEntry.GetComponent<object>()
		// System.Void GameFramework.Fsm.Fsm<object>.ChangeState<object>()
		// System.Void GameFramework.Fsm.FsmState<object>.ChangeState<object>(GameFramework.Fsm.IFsm<object>)
		// object GameFramework.Fsm.IFsm<object>.GetData<object>(string)
		// object GameFramework.Fsm.IFsm<object>.GetState<object>()
		// bool GameFramework.Fsm.IFsm<object>.HasState<object>()
		// System.Void GameFramework.Fsm.IFsm<object>.SetData<object>(string,object)
		// System.Void GameFramework.Fsm.IFsm<object>.Start<object>()
		// System.Void GameFramework.GameFrameworkLog.Error<object,object>(string,object,object)
		// System.Void GameFramework.GameFrameworkLog.Error<object>(string,object)
		// System.Void GameFramework.GameFrameworkLog.Info<int>(string,int)
		// System.Void GameFramework.GameFrameworkLog.Info<object,object,object,object>(string,object,object,object,object)
		// System.Void GameFramework.GameFrameworkLog.Info<object,object>(string,object,object)
		// System.Void GameFramework.GameFrameworkLog.Info<object>(string,object)
		// System.Void GameFramework.GameFrameworkLog.Warning<object,object>(string,object,object)
		// System.Void GameFramework.GameFrameworkLog.Warning<object>(string,object)
		// string GameFramework.Utility.Text.Format<int>(string,int)
		// string GameFramework.Utility.Text.Format<object,object,object,object>(string,object,object,object,object)
		// string GameFramework.Utility.Text.Format<object,object>(string,object,object)
		// string GameFramework.Utility.Text.Format<object>(string,object)
		// string GameFramework.Utility.Text.ITextHelper.Format<int>(string,int)
		// string GameFramework.Utility.Text.ITextHelper.Format<object,object,object,object>(string,object,object,object,object)
		// string GameFramework.Utility.Text.ITextHelper.Format<object,object>(string,object,object)
		// string GameFramework.Utility.Text.ITextHelper.Format<object>(string,object)
		// string Luban.StringUtil.CollectionToString<int>(System.Collections.Generic.IEnumerable<int>)
		// object& System.Runtime.CompilerServices.Unsafe.As<object,object>(object&)
		// System.Void* System.Runtime.CompilerServices.Unsafe.AsPointer<object>(object&)
		// object UnityEngine.Component.GetComponent<object>()
		// object UnityEngine.GameObject.AddComponent<object>()
		// object UnityEngine.GameObject.GetComponent<object>()
		// object UnityEngine.Object.FindFirstObjectByType<object>()
		// object UnityExtension.GetOrAddComponent<object>(UnityEngine.GameObject)
		// Cysharp.Threading.Tasks.UniTask<object> UnityGameFramework.Extension.Awaitable.LoadAssetAsync<object>(UnityGameFramework.Runtime.ResourceComponent,string,int,System.Threading.CancellationToken,System.Action<float>,System.Action<string>)
		// Cysharp.Threading.Tasks.UniTask<object> UnityGameFramework.Extension.Awaitable.NewUniTask<object>(UnityGameFramework.Extension.Awaitable.MoveNextFunc<object>,System.Action)
		// System.Void UnityGameFramework.Runtime.Log.Error<object,object>(string,object,object)
		// System.Void UnityGameFramework.Runtime.Log.Error<object>(string,object)
		// System.Void UnityGameFramework.Runtime.Log.Info<int>(string,int)
		// System.Void UnityGameFramework.Runtime.Log.Info<object,object,object,object>(string,object,object,object,object)
		// System.Void UnityGameFramework.Runtime.Log.Info<object,object>(string,object,object)
		// System.Void UnityGameFramework.Runtime.Log.Info<object>(string,object)
		// System.Void UnityGameFramework.Runtime.Log.Warning<object,object>(string,object,object)
		// System.Void UnityGameFramework.Runtime.Log.Warning<object>(string,object)
		// string string.Join<int>(string,System.Collections.Generic.IEnumerable<int>)
		// string string.JoinCore<int>(System.Char*,int,System.Collections.Generic.IEnumerable<int>)
	}
}