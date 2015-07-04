namespace NEventStore.Contrib.Persistence.AcceptanceTests.BDD
{
	using Xunit;

	[RunWith(typeof (SpecificationBaseRunner))]
    public abstract class SpecificationBase
    {
        protected virtual void Because()
        {}

        protected virtual void Cleanup()
        {}

        protected virtual void Context()
        {}

        public void OnFinish()
        {
            this.Cleanup();
        }

        public void OnStart()
        {
            this.Context();
            this.Because();
        }
    }
}