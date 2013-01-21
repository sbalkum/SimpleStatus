using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Practices.ServiceLocation;
using StructureMap;
using StructureMap.ServiceLocatorAdapter;

namespace SimpleStatus.IoC
{
	public class Bootstrapper
	{
		private static bool _hasStarted;

		public void BootstrapStructureMap()
		{
			ObjectFactory.Initialize(x =>
				                         {
					                         x.Scan(scanner =>
						                                {
							                                scanner.TheCallingAssembly();
							                                scanner.LookForRegistries();
							                                scanner.AddAllTypesOf<IStatusCheck>();

							                                scanner.WithDefaultConventions();
						                                });

				                         }
				);

			ObjectFactory.AssertConfigurationIsValid();

			ServiceLocator.SetLocatorProvider(() => new StructureMapServiceLocator(ObjectFactory.Container));
		}

		public static void Restart()
		{
			if (_hasStarted)
			{
				ObjectFactory.ResetDefaults();
			}
			else
			{
				InitializeStructureMap();
				_hasStarted = true;
			}
		}

		public static void InitializeStructureMap()
		{
			new Bootstrapper().BootstrapStructureMap();
		}
	}
}
