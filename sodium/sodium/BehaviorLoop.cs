namespace sodium
{

	public sealed class BehaviorLoop<A> : Behavior<A> {
		public BehaviorLoop() : base(new EventLoop<A>(), default(A)){
		}

		public void loop(Behavior<A> a_out)
		{
			((EventLoop<A>)event).loop(a_out.updates());
			value = a_out.sample();
		}
	}

}