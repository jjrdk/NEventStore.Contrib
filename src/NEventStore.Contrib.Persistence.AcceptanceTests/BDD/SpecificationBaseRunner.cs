namespace NEventStore.Contrib.Persistence.AcceptanceTests.BDD
{
	using System;
	using System.Collections.Generic;
	using System.Reflection;

	using Xunit;
	using Xunit.Sdk;

	internal class SpecificationBaseRunner : ITestClassCommand
    {
        private readonly List<object> _fixtures = new List<object>();
        private SpecificationBase _objectUnderTest;

        public SpecificationBase ObjectUnderTest
        {
            get
            {
                if (this._objectUnderTest == null)
                {
                    this.GuardTypeUnderTest();
                    this._objectUnderTest = (SpecificationBase) Activator.CreateInstance(this.TypeUnderTest.Type);
                }

                return this._objectUnderTest;
            }
        }

        object ITestClassCommand.ObjectUnderTest
        {
            get { return this.ObjectUnderTest; }
        }

        public ITypeInfo TypeUnderTest { get; set; }

        public int ChooseNextTest(ICollection<IMethodInfo> testsLeftToRun)
        {
            return 0;
        }

        public Exception ClassStart()
        {
            try
            {
                this.SetupFixtures();
                this.ObjectUnderTest.OnStart();
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        public Exception ClassFinish()
        {
            try
            {
                this.ObjectUnderTest.OnFinish();

                foreach (var fixtureData in this._fixtures)
                {
                    var disposable = fixtureData as IDisposable;
                    if (disposable != null)
                    {
                        disposable.Dispose();
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        public IEnumerable<ITestCommand> EnumerateTestCommands(IMethodInfo testMethod)
        {
            string displayName = (this.TypeUnderTest.Type.Name + ", it " + testMethod.Name).Replace('_', ' ');
            return new[] {new SpecTestCommand(testMethod, displayName)};
        }

        public IEnumerable<IMethodInfo> EnumerateTestMethods()
        {
            this.GuardTypeUnderTest();

            return TypeUtility.GetTestMethods(this.TypeUnderTest);
        }

        public bool IsTestMethod(IMethodInfo testMethod)
        {
            return MethodUtility.IsTest(testMethod);
        }

        private void SetupFixtures()
        {
            try
            {
                foreach (var @interface in this.TypeUnderTest.Type.GetInterfaces())
                {
                    if (@interface.IsGenericType)
                    {
                        Type genericDefinition = @interface.GetGenericTypeDefinition();

                        if (genericDefinition == typeof (IUseFixture<>))
                        {
                            Type dataType = @interface.GetGenericArguments()[0];
                            if (dataType == this.TypeUnderTest.Type)
                            {
                                throw new InvalidOperationException("Cannot use a test class as its own fixture data");
                            }

                            object fixtureData = null;

                            fixtureData = Activator.CreateInstance(dataType);

                            MethodInfo method = @interface.GetMethod("SetFixture", new[] {dataType});
                            this._fixtures.Add(fixtureData);
                            method.Invoke(this.ObjectUnderTest, new[] {fixtureData});
                        }
                    }
                }
            }
            catch (TargetInvocationException ex)
            {
                ExceptionUtility.RethrowWithNoStackTraceLoss(ex.InnerException);
            }
        }

        private void GuardTypeUnderTest()
        {
            if (this.TypeUnderTest == null)
            {
                throw new InvalidOperationException("Forgot to set TypeUnderTest before calling ObjectUnderTest");
            }

            if (!typeof (SpecificationBase).IsAssignableFrom(this.TypeUnderTest.Type))
            {
                throw new InvalidOperationException("SpecificationBaseRunner can only be used with types that derive from SpecificationBase");
            }
        }

        private class SpecTestCommand : TestCommand
        {
            public SpecTestCommand(IMethodInfo testMethod, string displayName) : base(testMethod, displayName, 0)
            {}

            public override MethodResult Execute(object testClass)
            {
                try
                {
                    this.testMethod.Invoke(testClass, null);
                }
                catch (ParameterCountMismatchException)
                {
                    throw new InvalidOperationException("Observation " + this.TypeName + "." + this.MethodName + " cannot have parameters");
                }

                return new PassedResult(this.testMethod, this.DisplayName);
            }
        }
    }
}