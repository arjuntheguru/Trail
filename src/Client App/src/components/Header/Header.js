import React, { useState } from 'react';
import { useSelector, useDispatch } from 'react-redux';
import { Link } from 'react-router-dom';
import { Navbar, Nav, Container, NavDropdown } from 'react-bootstrap';
import { logout } from './../../store/auth-actions';

import { FontAwesomeIcon } from '@fortawesome/react-fontawesome'
import { faBuilding, faStore, faTasks, faUser, faJedi, faSignOutAlt, faCog, faUserTie } from '@fortawesome/free-solid-svg-icons'

import ManagerPopover from './ManagerPopover';
import EditUser from './../../containers/Users/EditUser';


const Header = () => {

    const [dropdown1, setDropdown1] = useState(false);
    const [dropdown2, setDropdown2] = useState(false);

    const [showEditModal, setShowEditModal] = useState(false);
    const [editData, setEditData] = useState({});
    

    const editHandler = (data) => {

        setEditData(data);
        setShowEditModal(true);
        setDropdown2(!dropdown2)
    }

    const editModalCloseHandler = () => {
        setShowEditModal(false);
    }

    const dispatch = useDispatch();

    const auth = useSelector(state => state.auth)

    const logoutHandler = () => {
        setDropdown2(false)
        dispatch(logout());
    };

    return (
        <Navbar fixed='top' bg="primary" variant="dark">
            <Container fluid>
                {showEditModal ? <EditUser show={showEditModal} onClose={editModalCloseHandler} data={editData} /> : ''}
                <Link to="/" className='navbar-brand mt-1'><FontAwesomeIcon size="lg" icon={faJedi} className="" /></Link>
                {
                    auth.isAuthenticated ?
                        auth.user.role === "SuperAdmin" ?
                            <>
                                <Nav className="mr-auto mt-1">
                                    <Link to='/' className='nav-link'>Home</Link>
                                    <NavDropdown title="Manage" className="text-info" show={dropdown1} onToggle={() => { setDropdown1(!dropdown1) }}>
                                        <Link to='/company' className='dropdown-item' onClick={() => { setDropdown1(!dropdown1) }}>
                                            <FontAwesomeIcon icon={faBuilding} className="mr-2" /> Company
                                        </Link>
                                        <Link to='/task' className='dropdown-item' onClick={() => { setDropdown1(!dropdown1) }}>
                                            <FontAwesomeIcon icon={faTasks} className="mr-2" /> Task
                                        </Link>
                                    </NavDropdown>
                                </Nav>
                            </>
                            : auth.user.role === 'Admin' ?
                                <>
                                    <Nav className="mr-auto mt-1">
                                        <Link to='/' className='nav-link'>Home</Link>
                                        <NavDropdown title="Manage" className="text-info" show={dropdown1} onToggle={() => { setDropdown1(!dropdown1) }}>
                                            <Link to='/site' className='dropdown-item' onClick={() => { setDropdown1(!dropdown1) }}>
                                                <FontAwesomeIcon icon={faStore} className="mr-2" /> Sites
                                        </Link>                                       
                                        <Link to='/users' className='dropdown-item' onClick={() => { setDropdown1(!dropdown1) }}>
                                                <FontAwesomeIcon icon={faUser} className="mr-2" /> Users
                                        </Link>
                                        <Link to='/task' className='dropdown-item' onClick={() => { setDropdown1(!dropdown1) }}>
                                            <FontAwesomeIcon icon={faTasks} className="mr-2" /> Task
                                        </Link>
                                        </NavDropdown>
                                    </Nav>

                                </> :
                                auth.user.role === 'Manager' ?
                                    <>
                                        <Nav className="mr-auto mt-1">
                                            <Link to='/' className='nav-link'>Home</Link>
                                            <Link to='/site' className='nav-link'>Sites</Link>
                                        </Nav>
                                    </> :
                                    <>
                                        <Nav className="mr-auto mt-1">
                                            <Link to='/' className='nav-link'>Home</Link>
                                            <Link to={`/site/${auth.user.siteId[0]}`} className='nav-link'>Complete Tasks</Link>
                                        </Nav>
                                    </>
                        : ''

                }
                {auth.isAuthenticated && (
                    <Nav className="mt-1">
                        <NavDropdown title={<FontAwesomeIcon icon={faCog} size="lg" className="mr-2" />} className="text-info" show={dropdown2} onToggle={() => { setDropdown2(!dropdown2) }}>
                            <NavDropdown.Header>{auth.user.userName}</NavDropdown.Header>
                            <NavDropdown.Divider />
                            <a className='dropdown-item' onClick={() => { editHandler(auth.user) }} href="#">
                                <FontAwesomeIcon icon={faUserTie} className="mr-2" />Edit Profile
                                        </a>
                            {
                                auth.isAuthenticated &&
                                <Link to="/" className='dropdown-item' onClick={logoutHandler}> <FontAwesomeIcon icon={faSignOutAlt} className="mr-2" />
                                                    Log Out
                                                </Link>}
                        </NavDropdown>
                    </Nav>)}

                <Nav className="mt-1">
                    {!auth.isAuthenticated && <Link to="/" className='nav-link'>Login</Link>}
                </Nav>
            </Container>
        </Navbar>
    );
}

export default Header;