
using System;
using System.Collections.Generic;
using UnityEngine;

public static class ServiceLocator
{
	private static Dictionary<Type, object> services = new();
	public static void Register<T>(T service)
	{
		services[typeof(T)] = service;
	}
	public static T Get<T>()
	{
		return (T)services[typeof(T)];
	}
	public static void Reset() 
	{
		services.Clear();
	}
}

public interface IMapGenerator {
	void Generate();
}
public interface ISoundManager {
	void PlayBackgroundMusic();
}
public class SoundManager : ISoundManager {
	public void PlayBackgroundMusic() {
		Debug.Log("Playing background music...");
	}
}
public class DefaultMapGenerator : IMapGenerator {
	public void Generate() {
		Debug.Log("Generating default map...");
	}
}
public class RandomMapGenerator : IMapGenerator {
	public void Generate() {
		Debug.Log("Generating random map...");
	}
}

// public class GameManager : MonoBehaviour
// {
// 	void Awake()
// 	{
// 		ServiceLocator.Register<IMapGenerator>(new DefaultMapGenerator());
// 		ServiceLocator.Register<ISoundManager>(new SoundManager());
// 		
// 		StartGame();
// 	}
// 	void StartGame()
// 	{
// 		var mapGenerator = ServiceLocator.Get<IMapGenerator>();
// 		var soundManager = ServiceLocator.Get<ISoundManager>();
// 		
// 		mapGenerator.Generate();
// 		soundManager.PlayBackgroundMusic();
// 	}
// }

public class ExampleUsage : MonoBehaviour
{
	void Start()
	{
		var mapGenerator = ServiceLocator.Get<IMapGenerator>();
		var soundManager = ServiceLocator.Get<ISoundManager>();

		// var player = ServiceLocator.Get<IPlayer>();
		// var enemySpawner = ServiceLocator.Get<IEnemySpawner>();
		// var uiManager = ServiceLocator.Get<IUIManager>();
	}
}