import { Link } from 'react-router-dom'
export function NotFoundPage() { return <section className="state-panel"><div className="eyebrow">404</div><h1>That page stepped out.</h1><p>The address may be incorrect or the page may have moved.</p><Link className="button" to="/doctor">Back to dashboard</Link></section> }
