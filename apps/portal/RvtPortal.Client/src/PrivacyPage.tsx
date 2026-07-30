// File summary: Renders the public privacy policy document page.
// Major updates:
// - 2026-07-30 pending Extracted from App.tsx during the shell/page split.

import { ChevronLeft, ShieldCheck } from 'lucide-react';

type PrivacyPageProps = Readonly<{
  isAuthenticated: boolean;
  onNavigate: (path: string) => void;
}>;

// Function summary: Renders the PrivacyPage React component and wires its local UI behavior.
export function PrivacyPage({ isAuthenticated, onNavigate }: PrivacyPageProps) {
  return (
    <main className="auth-shell document-shell">
      <article className="document-panel" aria-label="Privacy policy">
        <div className="document-heading">
          <ShieldCheck size={28} aria-hidden="true" />
          <div>
            <p>RVT Group</p>
            <h1>Privacy Policy</h1>
          </div>
        </div>
        <p>
          Your privacy is important to RVT Group, and this privacy policy describes how we collect, use, disclose,
          transfer, and store your information. We will take all reasonable steps to ensure that your data is treated
          securely and in accordance with this privacy policy.
        </p>
        <p>RVT Group complies with its obligations under the General Data Protection Regulation by:</p>
        <ul>
          <li>keeping the data it holds up to date,</li>
          <li>storing and destroying it securely,</li>
          <li>not collecting or retaining excessive amounts of data,</li>
          <li>protecting personal data from loss, misuse, unauthorised access and disclosure, and</li>
          <li>ensuring that appropriate technical measures are in place to protect personal data.</li>
        </ul>
        <h2>The reason we collect and process information</h2>
        <p>
          We process personal information to enable us to promote our goods and services, to maintain our accounts and
          records, and to support and manage our staff. If you are a customer (or potential customer), information about
          you helps us to:
        </p>
        <ul>
          <li>
            provide you with information, products or services that you request from us or which we feel may interest
            you, and
          </li>
          <li>carry out our obligations arising from any contracts entered into between you and us.</li>
        </ul>
        <h2>The data we collect</h2>
        <p>We process information relevant to the above reasons/purposes. This may include:</p>
        <ul>
          <li>
            personal details such as name, work email address, mobile phone number, landline phone number, job title
            (but not information categorised as sensitive under data protection laws and regulations)
          </li>
          <li>
            employment details such as such as name, work email address, mobile phone number, landline phone number, job
            title
          </li>
          <li>goods or services provided (by us to you, or by you to us)</li>
        </ul>
        <p>
          We may collect certain information or data about you in the course of business, such as when you visit our
          website, contact us directly, or engage with our email bulletins (e.g. tracking whether you open these emails
          and what links you may click on). Such data could include your name, address, telephone number, email address
          and social media identifiers.
        </p>
        <p>
          If you telephone us, your call will not be recorded, but may be monitored by RVT personnel for the purposes of
          training to ensure that the highest possible quality of service is provided.
        </p>
        <p>
          Your information can be viewed by authorised people within RVT Group and relevant trustworthy external
          agencies supporting normal business operation, and may be used to:
        </p>
        <ul>
          <li>improve our website by monitoring how you use it</li>
          <li>gather feedback to improve our services and our email bulletins</li>
          <li>despatch goods to you</li>
          <li>respond to any feedback you send us</li>
        </ul>
        <p>
          RVT Group is dedicated to protecting people's health on and near construction and demolition sites against
          hazards such as dust, fumes and noise with temporary-environment control. Our lawful basis for collecting and
          processing your information is that it is of legitimate interest:
        </p>
        <ol>
          <li>
            to provide you with information that could help to protect the health of people affected by site activity,
          </li>
          <li>
            to provide you with information that will help to protect the environment at large from dust, fumes and
            noise, and
          </li>
          <li>to help us to grow our business.</li>
        </ol>
        <p>
          The above interests were identified as a result of a legitimate interests assessment that we have conducted.
        </p>
        <h2>Requests for additional information</h2>
        <p>
          Sometimes we will require you to provide further personal information. This may be if you are hiring equipment
          from us. Whenever we do this, we will tell you why we are collecting this information and how we will use it.
        </p>
        <h2>IP addresses</h2>
        <p>
          If you contact us online, we may monitor the type of device used by you. This may include specific
          identification, such as your IP address.
        </p>
        <h2>How we use this information</h2>
        <p>
          We do not sell customer's personal data to third parties and will only use your personal information to
          provide you with details of our own products, or services which we believe will be of interest to you. RVT
          Group use email addresses to personalise and improve digital marketing campaigns.
        </p>
        <h2>Where your information is stored</h2>
        <p>
          We store your information on secure servers within the UK (and so within the European Economic Area or EEA).
          The email platform we use, Mailchimp, is based outside the EEA and their servers hold your information in the
          United States. MailChimp participates in, and has certified its compliance with, the EU-U.S. Privacy Shield
          Framework.
        </p>
        <h2>Keeping your information secure</h2>
        <p>
          We have procedures and security features in place to keep your information secure once we receive it. For
          example:
        </p>
        <ul>
          <li>
            All log in users to our server are password protected and passwords have to be changed by default every 90
            days.
          </li>
          <li>All employees and anyone accessing data have to sign a confidentiality agreement.</li>
          <li>Employee records can only be accessed by directors and approved senior personnel within the company</li>
          <li>Users have varying degrees of access depending on their position within the company</li>
          <li>Only directors and approved senior personnel can access the memory stick port on the computers</li>
        </ul>
        <p>
          Our company website uses HyperText Transfer Protocol Secure (HTPPS) coding on all its pages to help keep your
          information safe from hackers and, like most websites, uses cookies to enhance visitor experience (by, for
          example, enabling pages to load faster) and provide information about the aggregated statistics on how our
          website is used. The cookies we use do not obtain data that identifies individuals.
        </p>
        <h2>Disclosing your information</h2>
        <p>
          We may pass on your personal information if we have a legal obligation to do so, or if we have to enforce or
          apply our terms of use and other agreements. This may include disclosing your information to other companies
          and organisations in connection with fraud protection and credit risk reduction. We may also share your
          information with relevant external third parties for the following reasons:
        </p>
        <ul>
          <li>
            Marketing Agencies: To ensure our database is kept current and up to date, if additional resource is
            required.
          </li>
          <li>
            Marketing Platforms and Apps: To ensure that we are able to communicate seamlessly with you across multiple
            channels.
          </li>
          <li>
            Consultancy: To ensure the long-term sustainability of RVT Group by continually providing a relevant, high
            quality service offering.
          </li>
          <li>Logistics Agencies: To ensure our equipment is delivered on time.</li>
        </ul>
        <p>They will not pass on your information to other parties.</p>
        <h2>Third parties</h2>
        <p>
          We do not allow the information we hold about you to be used for advertising purposes or contact from third
          parties.
        </p>
        <h2>Cookies</h2>
        <p>
          By using our website you signify your agreement to our use of cookies. Our website uses cookies to store
          information on your computer. Some cookies on our site are essential, and the site won't work as expected
          without them. These cookies are set when you interact with the site by doing something that goes beyond
          clicking on simple links.
        </p>
        <p>
          We also use some non-essential 'performance' cookies, such as Google Analytics and Add This sharing feature,
          to anonymously track visitors or enhance your experience of the site. If you wish to restrict or block web
          browser cookies which are set on your device then you can do this through your browser settings. Click on the
          Help function within your browser to find out more.
        </p>
        <p>Performance cookies:</p>
        <ul>
          <li>These cookies are used to measure the performance of websites and see how websites are used.</li>
          <li>We use 'Performance' cookies to improve how the website works and measure our marketing activity.</li>
          <li>
            Information that is collected using these cookies is aggregated and anonymous and we are not able to
            identify individual users with these cookies.
          </li>
        </ul>
        <p>On www.rvtgroup.co.uk we may use 'Performance' cookies to:</p>
        <ul>
          <li>Provide us with aggregated statistics on how our website is used.</li>
          <li>
            Provide feedback to partners that one of our visitors also visited their website. This lets our partners
            improve their websites. We don't allow our partners to reuse this information for further advertising.
          </li>
          <li>
            Help us improve the website by measuring any errors that occur and also to improve the performance of the
            site.
          </li>
          <li>Test different designs of pages on our website.</li>
        </ul>
        <p>
          Cookies we have defined as 'Performance' cookies will NOT be used to remember any preferences you have set
          beyond the current visit.
        </p>
        <h2>Changes to our privacy and cookies policy</h2>
        <p>
          We may make changes and update our privacy and cookies policy from time to time and in accordance with updated
          legislation. Any such changes will be shown here as part of our privacy and cookies policy and will apply from
          the date that they are published. We are unable to contact you directly to inform you of these changes, other
          than in response to a specific request made to us as referred to above.
        </p>
        <h2>Your rights</h2>
        <p>
          You can find out what information we hold about you and ask us not to use any of the information we collect.
          If you wish to exercise this right, please send your request to our Marketing Department in writing by email
          to <a href="mailto:dataprotection@rvtgroup.co.uk">dataprotection@rvtgroup.co.uk</a>
        </p>
        <p>or by post to:</p>
        <p>
          RVT Group
          <br />
          Prospect House,
          <br />
          Riverside Way,
          <br />
          Dartford,
          <br />
          Kent,
          <br />
          DA1 5BS
        </p>
        <p>
          If you wish to unsubscribe from our email bulletins you can also do this by clicking on the unsubscribe link
          each one contains.
        </p>
        <h2>About Us</h2>
        <p>
          RVT Group is a limited company, registered company number 07907482, and registered office address Prospect
          House Riverside Industrial Estate, Riverside Way, Dartford, Kent, DA1 5BS.
        </p>
        <p>
          Please note that if you click on, or follow, any links from our site to external websites, our privacy policy
          will no longer apply. Please check the privacy policies of any such external site before submitting any
          personal data, as we cannot accept any responsibility or liability in relation to them.
        </p>
        <button className="secondary-button" type="button" onClick={() => onNavigate(isAuthenticated ? '/' : '/login')}>
          <ChevronLeft size={17} aria-hidden="true" />
          <span>{isAuthenticated ? 'Back to dashboard' : 'Back to sign in'}</span>
        </button>
      </article>
    </main>
  );
}
