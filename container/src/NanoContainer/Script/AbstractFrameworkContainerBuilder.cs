using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.IO;
using System.Reflection;
using NanoContainer.IntegrationKit;
using PicoContainer;

namespace NanoContainer.Script
{
	public abstract class AbstractFrameworkContainerBuilder : ScriptedContainerBuilder
	{
		private readonly string PARENT_PROPERTY = "Parent";
		private readonly string COMPOSE_METHOD = "Compose";
		private FrameworkCompiler frameworkCompiler = new FrameworkCompiler();

		public AbstractFrameworkContainerBuilder(StreamReader script) : base(script)
		{
		}

		protected FrameworkCompiler FrameworkCompiler
		{
			get { return frameworkCompiler; }
		}

		protected override IMutablePicoContainer CreateContainerFromScript(IPicoContainer parentContainer,
		                                                                   object assemblyScope)
		{
			IList assemblies = assemblyScope as IList;
			Assembly dynamicAssembly = this.GetCompiledAssembly(StreamReader, assemblies);
			Type compiledType = GetCompiledType(dynamicAssembly);
			object instance = dynamicAssembly.CreateInstance(compiledType.FullName);

			RegisterParentToContainerScript(parentContainer, instance);

			MethodInfo methodInfo = GetComposeMethod(compiledType);
			return methodInfo.Invoke(instance, new object[] {}) as IMutablePicoContainer;
		}

		private void RegisterParentToContainerScript(IPicoContainer parentContainer, object instance)
		{
			if (parentContainer != null)
			{
				PropertyInfo parentProperty = instance.GetType().GetProperty(PARENT_PROPERTY);
				if (parentProperty == null)
				{
					string errorMsg = "A parent container is provided but the composition script has no " + 
								   "property defined for accepting the parent.\n" +
						           "Please specify a property called Parent and it should be of the " +
						           "type PicoContainer.";
					throw new PicoCompositionException(errorMsg);
				}
				parentProperty.SetValue(instance, parentContainer, new object[] {});
			}
		}

		protected virtual Assembly GetCompiledAssembly(StreamReader scriptCode, IList assemblies)
		{
			return FrameworkCompiler.Compile(CodeDomProvider, scriptCode, assemblies);
		}

		protected virtual Type GetCompiledType(Assembly assembly)
		{
			foreach (Type type in assembly.GetTypes())
			{
				if (HasValidConstructorAndComposeMethod(type))
				{
					return type;
				}
			}
			throw new PicoCompositionException("The script is not usable. Please specify a correct script.");
		}

		protected abstract CodeDomProvider CodeDomProvider { get; }

		protected bool HasValidConstructorAndComposeMethod(Type type)
		{
			ConstructorInfo constructorInfo = type.GetConstructor(new Type[] {});
			if (constructorInfo == null)
			{
				return false;
			}

			MethodInfo methodInfo = GetComposeMethod(type);
			if (methodInfo == null || methodInfo.ReturnType != typeof (IMutablePicoContainer))
			{
				return false;
			}

			return true;
		}

		private MethodInfo GetComposeMethod(Type type)
		{
			return type.GetMethod(COMPOSE_METHOD);
		}
	}
}